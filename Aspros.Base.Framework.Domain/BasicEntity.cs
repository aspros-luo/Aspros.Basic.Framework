using Aspros.Base.Framework.Domain.ValueObjects;

﻿
namespace Aspros.Base.Framework.Domain;

public class BasicEntity
{
    public long Creator { get; set; } = 0;
    public DateTime GmtCreated { get; set; } = DateTime.Now;
    public long Modifier { get; set; } = 0;
    public DateTime GmtModified { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
    /// <summary>
    /// 状态，0：正常；-1：删除；-2：停用
    /// </summary>
    public Status EntityStatus Status { get; protected set; } = EntityStatus.Normal;
}
