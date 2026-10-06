/** Copyright 2010-2012 Twitter, Inc.*/
/**
 * Snowflake-style distributed ID generator.
 * 基于 Snowflake 思路的分布式 ID 生成器。
 *
 * <para>
 * The generated 64-bit value combines timestamp, datacenter, worker and sequence bits.
 * 生成的 64 位 ID 由时间戳、数据中心、机器节点和毫秒内序列号组成。
 * </para>
 */
namespace Aspros.Base.Framework.Infrastructure
{
    /// <summary>
    /// Generates unique numeric IDs for one worker/datacenter combination.
    /// 为一个“数据中心 + Worker”组合生成唯一数字 ID。
    /// </summary>
    public class IdWorker
    {
        //基准时间 / Custom epoch used to reduce the timestamp portion.
        public const long Twepoch = 1288834974657L;
        //机器标识位数 / Number of worker-id bits.
        const int WorkerIdBits = 5;
        //数据标志位数 / Number of datacenter-id bits.
        const int DatacenterIdBits = 5;
        //序列号位数 / Number of per-millisecond sequence bits.
        const int SequenceBits = 12;
        //机器ID最大值 / Maximum worker ID.
        const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);
        //数据标志ID最大值 / Maximum datacenter ID.
        const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);
        //序列号最大值 / Maximum sequence value.
        private const long SequenceMask = -1L ^ (-1L << SequenceBits);
        //机器ID左移12位 / Worker ID shift.
        private const int WorkerIdShift = SequenceBits;
        //数据ID左移17位 / Datacenter ID shift.
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;
        //时间毫秒左移22位 / Timestamp shift.
        public const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits;

        private long _sequence = 0L;
        private long _lastTimestamp = -1L;

        /// <summary>
        /// Worker identifier within the configured 5-bit range.
        /// 当前 Worker 节点 ID，范围由 5 位 WorkerId 决定。
        /// </summary>
        public long WorkerId { get; protected set; }

        /// <summary>
        /// Datacenter identifier within the configured 5-bit range.
        /// 当前数据中心 ID，范围由 5 位 DatacenterId 决定。
        /// </summary>
        public long DatacenterId { get; protected set; }

        /// <summary>
        /// Sequence number within the current millisecond.
        /// 当前毫秒内的序列号。
        /// </summary>
        public long Sequence
        {
            get { return _sequence; }
            internal set { _sequence = value; }
        }

        /// <summary>
        /// Creates a generator with validated worker, datacenter and sequence values.
        /// 创建 ID 生成器，并校验 Worker、Datacenter 和初始 Sequence。
        /// </summary>
        public IdWorker(long workerId, long datacenterId, long sequence = 0L)
        {
            if (sequence < 0 || sequence > SequenceMask)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sequence),
                    sequence,
                    $"Sequence must be between 0 and {SequenceMask}.");
            }

            if (workerId > MaxWorkerId || workerId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(workerId),
                    workerId,
                    $"WorkerId must be between 0 and {MaxWorkerId}.");
            }

            if (datacenterId > MaxDatacenterId || datacenterId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(datacenterId),
                    datacenterId,
                    $"DatacenterId must be between 0 and {MaxDatacenterId}.");
            }

            WorkerId = workerId;
            DatacenterId = datacenterId;
            _sequence = sequence;
        }

        readonly object _lock = new();

        /// <summary>
        /// Generates the next ID in a thread-safe critical section.
        /// 在线程安全的临界区内生成下一个 ID。
        ///
        /// <para>
        /// If more than 4096 IDs are requested in one millisecond, generation waits
        /// for the next millisecond because only 12 bits are available for Sequence.
        /// 如果同一毫秒生成超过 4096 个 ID，会等待下一毫秒，因为 Sequence 只有 12 位。
        /// Clock rollback is rejected instead of generating a potentially conflicting ID.
        /// 如果系统时钟回拨，则直接拒绝生成，而不是冒险产生潜在冲突的 ID。
        /// </para>
        /// </summary>
        public virtual long NextId()
        {
            lock (_lock)
            {
                var timestamp = TimeGen();

                if (timestamp < _lastTimestamp)
                {
                    throw new Exception(string.Format(
                        "时间戳必须大于上一次生成ID的时间戳.  拒绝为{0}毫秒生成id",
                        _lastTimestamp - timestamp));
                }

                //如果上次生成时间和当前时间相同,在同一毫秒内
                if (_lastTimestamp == timestamp)
                {
                    //sequence自增，和sequenceMask相与一下，去掉高位
                    _sequence = (_sequence + 1) & SequenceMask;

                    // The 12-bit sequence overflows at 4096 IDs in the same millisecond.
                    if (_sequence == 0)
                    {
                        //等待到下一毫秒
                        timestamp = TilNextMillis(_lastTimestamp);
                    }
                }
                else
                {
                    //如果和上次生成时间不同,重置sequence
                    _sequence = 0;
                }

                _lastTimestamp = timestamp;

                return ((timestamp - Twepoch) << TimestampLeftShift)
                       | (DatacenterId << DatacenterIdShift)
                       | (WorkerId << WorkerIdShift)
                       | _sequence;
            }
        }

        /// <summary>
        /// Waits until the system clock reaches a millisecond after the previous timestamp.
        /// 等待系统时间进入上一个时间戳之后的毫秒。
        /// </summary>
        protected virtual long TilNextMillis(long lastTimestamp)
        {
            var timestamp = TimeGen();

            while (timestamp <= lastTimestamp)
            {
                timestamp = TimeGen();
            }

            return timestamp;
        }

        /// <summary>
        /// Gets the current timestamp through the framework time provider.
        /// 通过 Framework 时间提供器获取当前时间戳。
        /// </summary>
        protected virtual long TimeGen()
        {
            return TimeExtensions.CurrentTimeMillis();
        }
    }
}