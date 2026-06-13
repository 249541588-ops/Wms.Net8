namespace Wms.Core.Domain.Interfaces;

/// <summary>
/// 可审计实体接口 - 包含创建和修改信息
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTime? CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    string? ModifiedBy { get; set; }
}

/// <summary>
/// 有版本号的实体接口
/// </summary>
public interface IVersioned
{
    /// <summary>
    /// 版本号（用于乐观并发控制）
    /// </summary>
    int Version { get; set; }
}

/// <summary>
/// 实体基础接口
/// </summary>
public interface IEntity
{
    /// <summary>
    /// 实体 ID
    /// </summary>
    object Id { get; }
}

/// <summary>
/// 强类型 ID 实体接口
/// </summary>
/// <typeparam name="TKey">ID 类型</typeparam>
public interface IEntity<TKey> : IEntity
{
    /// <summary>
    /// 实体 ID
    /// </summary>
    new TKey Id { get; set; }
}
