import fs from 'fs';
import path from 'path';
import { DatabaseSync } from 'node:sqlite';
import type {
  OrderAggregate,
  InventoryEntity,
  PaymentEntity,
  StagedChange,
  CapOutboxMessage,
  OrderItem,
} from '../types';

export class DatabaseEngine {
  private db: DatabaseSync;
  private dbFilePath: string;
  private totalWritesCount = 0;

  constructor() {
    const dataDir = path.resolve(process.cwd(), 'data');
    if (!fs.existsSync(dataDir)) {
      fs.mkdirSync(dataDir, { recursive: true });
    }
    this.dbFilePath = path.join(dataDir, 'aspros_production.db');
    this.db = new DatabaseSync(this.dbFilePath);
    this.initSchema();
  }

  private initSchema(): void {
    // Enable WAL mode and foreign keys for high concurrency
    try {
      this.db.exec(`
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS inventories (
          sku TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          tenant_id INTEGER NOT NULL,
          available_stock INTEGER NOT NULL,
          locked_stock INTEGER NOT NULL DEFAULT 0,
          safety_threshold INTEGER NOT NULL DEFAULT 10,
          updated_time TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS orders (
          id TEXT PRIMARY KEY,
          tenant_id INTEGER NOT NULL,
          customer_name TEXT NOT NULL,
          items_json TEXT NOT NULL,
          total_amount REAL NOT NULL,
          currency TEXT NOT NULL DEFAULT 'CNY',
          status TEXT NOT NULL,
          deleted INTEGER NOT NULL DEFAULT 0,
          created_time TEXT NOT NULL,
          updated_time TEXT NOT NULL,
          idempotency_key TEXT
        );

        CREATE TABLE IF NOT EXISTS payments (
          payment_id TEXT PRIMARY KEY,
          order_id TEXT NOT NULL,
          tenant_id INTEGER NOT NULL,
          amount REAL NOT NULL,
          currency TEXT NOT NULL DEFAULT 'CNY',
          channel TEXT NOT NULL,
          status TEXT NOT NULL,
          transaction_no TEXT NOT NULL,
          created_time TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS cap_messages (
          id TEXT PRIMARY KEY,
          topic TEXT NOT NULL,
          contract_type TEXT NOT NULL,
          payload_json TEXT NOT NULL,
          status TEXT NOT NULL,
          retry_count INTEGER NOT NULL DEFAULT 0,
          created_time TEXT NOT NULL,
          last_retry_time TEXT
        );

        CREATE TABLE IF NOT EXISTS idempotency_keys (
          key TEXT PRIMARY KEY,
          tenant_id INTEGER NOT NULL,
          status TEXT NOT NULL,
          receipt_json TEXT NOT NULL,
          created_time TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_orders_tenant ON orders (tenant_id, deleted);
        CREATE INDEX IF NOT EXISTS idx_inventories_tenant ON inventories (tenant_id);
      `);

      this.seedInitialDataIfEmpty();
    } catch (err) {
      console.error('[DatabaseEngine] Error initializing schema:', err);
    }
  }

  private seedInitialDataIfEmpty(): void {
    const row = this.db.prepare('SELECT count(*) as count FROM inventories').get() as { count: number };
    if (row && row.count > 0) {
      return;
    }

    // Seed initial inventories
    const insertInv = this.db.prepare(`
      INSERT INTO inventories (sku, name, tenant_id, available_stock, locked_stock, safety_threshold, updated_time)
      VALUES (?, ?, ?, ?, ?, ?, ?)
    `);

    const invs = [
      ['SKU-PRO-SERVER-16C', '高密分布式计算节点 16C/64G', 100, 48, 5, 10, new Date().toISOString()],
      ['SKU-GPU-ACCEL-A100', 'AI 训推加速卡 80GB SXM4', 100, 12, 2, 5, new Date().toISOString()],
      ['SKU-IOT-GATEWAY-V2', '工业边缘物联智能网关 V2', 200, 150, 0, 20, new Date().toISOString()],
      ['SKU-EDGE-ROUTER-X', '高可靠双频边缘工业路由 X-900', 200, 80, 4, 15, new Date().toISOString()],
    ];

    for (const inv of invs) {
      insertInv.run(...inv);
    }

    // Seed initial orders
    const insertOrder = this.db.prepare(`
      INSERT INTO orders (id, tenant_id, customer_name, items_json, total_amount, currency, status, deleted, created_time, updated_time, idempotency_key)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    `);

    const orders = [
      [
        'ORD-20261018-001',
        100,
        '华东车联网调度中心',
        JSON.stringify([{ sku: 'SKU-PRO-SERVER-16C', title: '高密分布式计算节点 16C/64G', unitPrice: 4800, quantity: 2 }]),
        9600,
        'CNY',
        'Paid',
        0,
        new Date(Date.now() - 7200000).toISOString(),
        new Date(Date.now() - 7100000).toISOString(),
        'IDEMP-20261018-001',
      ],
      [
        'ORD-20261018-002',
        100,
        '智算超算科研实验室',
        JSON.stringify([{ sku: 'SKU-GPU-ACCEL-A100', title: 'AI 训推加速卡 80GB SXM4', unitPrice: 28500, quantity: 1 }]),
        28500,
        'CNY',
        'Processing',
        0,
        new Date(Date.now() - 3600000).toISOString(),
        new Date(Date.now() - 3500000).toISOString(),
        'IDEMP-20261018-002',
      ],
      [
        'ORD-20261018-003',
        200,
        '华北智能制造一厂',
        JSON.stringify([{ sku: 'SKU-IOT-GATEWAY-V2', title: '工业边缘物联智能网关 V2', unitPrice: 1200, quantity: 5 }]),
        6000,
        'CNY',
        'Draft',
        0,
        new Date(Date.now() - 1800000).toISOString(),
        new Date(Date.now() - 1800000).toISOString(),
        'IDEMP-20261018-003',
      ],
    ];

    for (const ord of orders) {
      insertOrder.run(...ord);
    }

    // Seed initial payments
    const insertPay = this.db.prepare(`
      INSERT INTO payments (payment_id, order_id, tenant_id, amount, currency, channel, status, transaction_no, created_time)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
    `);

    insertPay.run(
      'PAY-20261018-01',
      'ORD-20261018-001',
      100,
      9600,
      'CNY',
      'CorporateTransfer',
      'Success',
      'TXN-ABC-992100',
      new Date(Date.now() - 7000000).toISOString()
    );
    insertPay.run(
      'PAY-20261018-02',
      'ORD-20261018-002',
      100,
      28500,
      'CNY',
      'UnionPay',
      'Success',
      'TXN-ABC-992101',
      new Date(Date.now() - 3400000).toISOString()
    );

    // Seed initial CAP Outbox messages
    const insertCap = this.db.prepare(`
      INSERT INTO cap_messages (id, topic, contract_type, payload_json, status, retry_count, created_time, last_retry_time)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?)
    `);

    insertCap.run(
      'CAP-MSG-001',
      'aspros.order.dispatched',
      'IIntegrationEvent',
      JSON.stringify({ orderId: 'ORD-20261018-001', logisticsProvider: 'SF_EXPRESS' }),
      'Sent',
      0,
      new Date(Date.now() - 7100000).toISOString(),
      new Date(Date.now() - 7100000).toISOString()
    );

    insertCap.run(
      'CAP-MSG-002',
      'aspros.inventory.deducted',
      'IIntegrationEvent',
      JSON.stringify({ sku: 'SKU-PRO-SERVER-16C', quantity: 2, tenantId: 100 }),
      'Sent',
      0,
      new Date(Date.now() - 7100000).toISOString(),
      new Date(Date.now() - 7100000).toISOString()
    );

    insertCap.run(
      'CAP-MSG-003',
      'aspros.payment.reconciled',
      'IIntegrationEvent',
      JSON.stringify({ paymentId: 'PAY-20261018-01', amount: 9600 }),
      'Sent',
      0,
      new Date(Date.now() - 7000000).toISOString(),
      new Date(Date.now() - 7000000).toISOString()
    );
  }

  public getOrders(tenantId?: number | null): OrderAggregate[] {
    let query = 'SELECT * FROM orders WHERE deleted = 0';
    const params: any[] = [];
    if (tenantId) {
      query += ' AND tenant_id = ?';
      params.push(tenantId);
    }
    query += ' ORDER BY created_time DESC';

    const rows = this.db.prepare(query).all(...params) as any[];
    return rows.map((r) => ({
      id: r.id,
      tenantId: r.tenant_id,
      customerName: r.customer_name,
      items: JSON.parse(r.items_json || '[]') as OrderItem[],
      totalAmount: r.total_amount,
      currency: r.currency,
      status: r.status,
      deleted: Boolean(r.deleted),
      createdTime: r.created_time,
      updatedTime: r.updated_time,
      idempotencyKey: r.idempotency_key,
    }));
  }

  public getInventories(tenantId?: number | null): InventoryEntity[] {
    let query = 'SELECT * FROM inventories';
    const params: any[] = [];
    if (tenantId) {
      query += ' WHERE tenant_id = ?';
      params.push(tenantId);
    }
    query += ' ORDER BY sku ASC';

    const rows = this.db.prepare(query).all(...params) as any[];
    return rows.map((r) => ({
      sku: r.sku,
      name: r.name,
      tenantId: r.tenant_id,
      availableStock: r.available_stock,
      lockedStock: r.locked_stock,
      safetyThreshold: r.safety_threshold,
      updatedTime: r.updated_time,
    }));
  }

  public getPayments(tenantId?: number | null): PaymentEntity[] {
    let query = 'SELECT * FROM payments';
    const params: any[] = [];
    if (tenantId) {
      query += ' WHERE tenant_id = ?';
      params.push(tenantId);
    }
    query += ' ORDER BY created_time DESC';

    const rows = this.db.prepare(query).all(...params) as any[];
    return rows.map((r) => ({
      paymentId: r.payment_id,
      orderId: r.order_id,
      tenantId: r.tenant_id,
      amount: r.amount,
      currency: r.currency,
      channel: r.channel,
      status: r.status,
      transactionNo: r.transaction_no,
      createdTime: r.created_time,
    }));
  }

  public getCapMessages(): CapOutboxMessage[] {
    const rows = this.db.prepare('SELECT * FROM cap_messages ORDER BY created_time DESC LIMIT 50').all() as any[];
    return rows.map((r) => {
      let content = {};
      try {
        content = JSON.parse(r.payload_json || '{}');
      } catch {
        content = { raw: r.payload_json };
      }
      return {
        id: r.id,
        topic: r.topic,
        group: 'aspros.cap.consumer.default',
        content,
        status: (r.status === 'Succeeded' ? 'Sent' : r.status) as 'Pending' | 'Sent' | 'Consumed' | 'Failed',
        retryCount: r.retry_count || 0,
        createdTime: r.created_time,
        transactionId: r.id,
        contractType: r.contract_type as any,
      };
    });
  }

  /**
   * Real atomic transaction with SQLite BEGIN IMMEDIATE & COMMIT
   */
  public commitUnitOfWork(order: OrderAggregate, stagedChanges: StagedChange[]): void {
    this.db.exec('BEGIN IMMEDIATE TRANSACTION');
    try {
      // 1. Insert Order
      const insertOrder = this.db.prepare(`
        INSERT OR REPLACE INTO orders (id, tenant_id, customer_name, items_json, total_amount, currency, status, deleted, created_time, updated_time, idempotency_key)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
      `);
      insertOrder.run(
        order.id,
        order.tenantId,
        order.customerName,
        JSON.stringify(order.items),
        order.totalAmount,
        order.currency,
        order.status,
        order.deleted ? 1 : 0,
        order.createdTime,
        order.updatedTime,
        order.idempotencyKey || null
      );

      // 2. Process Staged Changes (e.g. inventory deductions)
      const updateInv = this.db.prepare(`
        UPDATE inventories 
        SET available_stock = available_stock - ?, locked_stock = locked_stock + ?, updated_time = ?
        WHERE sku = ? AND available_stock >= ?
      `);

      for (const change of stagedChanges) {
        if (change.type === 'Update' && change.entity.sku) {
          const qty = change.entity.quantity || 1;
          const info = updateInv.run(qty, qty, new Date().toISOString(), change.entity.sku, qty);
          if (info.changes === 0) {
            throw new Error(`库存不足或并发更新冲突: SKU ${change.entity.sku}`);
          }
        }
      }

      // 3. Atomically write to CAP Outbox
      const insertCap = this.db.prepare(`
        INSERT INTO cap_messages (id, topic, contract_type, payload_json, status, retry_count, created_time, last_retry_time)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
      `);
      insertCap.run(
        `CAP-ORD-${Date.now().toString(36).toUpperCase()}`,
        'aspros.order.created',
        'IIntegrationEvent',
        JSON.stringify({ orderId: order.id, totalAmount: order.totalAmount, tenantId: order.tenantId }),
        'Sent',
        0,
        new Date().toISOString(),
        new Date().toISOString()
      );

      this.db.exec('COMMIT');
      this.totalWritesCount++;
    } catch (err) {
      this.db.exec('ROLLBACK');
      throw err;
    }
  }

  public publishCapMessage(topic: string, payload: Record<string, any>, contractType: string): CapOutboxMessage {
    const id = `CAP-PUB-${Date.now().toString(36).toUpperCase()}`;
    const now = new Date().toISOString();
    const insertCap = this.db.prepare(`
      INSERT INTO cap_messages (id, topic, contract_type, payload_json, status, retry_count, created_time, last_retry_time)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?)
    `);

    insertCap.run(id, topic, contractType, JSON.stringify(payload), 'Sent', 0, now, now);
    this.totalWritesCount++;

    return {
      id,
      topic,
      group: 'aspros.cap.consumer.default',
      content: payload,
      contractType: contractType as any,
      status: 'Sent',
      retryCount: 0,
      createdTime: now,
      transactionId: id,
    };
  }

  public retryCapMessage(id: string): void {
    const update = this.db.prepare(`
      UPDATE cap_messages 
      SET status = 'Sent', retry_count = retry_count + 1, last_retry_time = ?
      WHERE id = ?
    `);
    update.run(new Date().toISOString(), id);
    this.totalWritesCount++;
  }

  public getDatabaseStats() {
    let fileSize = 0;
    try {
      const stats = fs.statSync(this.dbFilePath);
      fileSize = stats.size;
    } catch {}

    const orderCount = (this.db.prepare('SELECT count(*) as count FROM orders').get() as any)?.count || 0;
    const invCount = (this.db.prepare('SELECT count(*) as count FROM inventories').get() as any)?.count || 0;
    const payCount = (this.db.prepare('SELECT count(*) as count FROM payments').get() as any)?.count || 0;
    const capCount = (this.db.prepare('SELECT count(*) as count FROM cap_messages').get() as any)?.count || 0;

    return {
      engine: 'SQLite 3.45 (Node 22 native node:sqlite DatabaseSync)',
      driver: 'Persistent Disk File (data/aspros_production.db)',
      filePath: this.dbFilePath,
      fileSizeBytes: fileSize,
      journalMode: 'WAL (Write-Ahead Logging)',
      totalRecords: orderCount + invCount + payCount + capCount,
      tableCounts: {
        orders: orderCount,
        inventories: invCount,
        payments: payCount,
        capMessages: capCount,
      },
      totalWritesCount: this.totalWritesCount,
      status: 'CONNECTED',
    };
  }
}

export const databaseEngine = new DatabaseEngine();
