using Riok.Mapperly.Abstractions;
using Wms.Core.Application.DTOs;
using Wms.Core.Domain.Entities;

namespace Wms.Core.Infrastructure.Mappers;

/// <summary>
/// WMS 全局对象映射器（Mapperly 源码生成）
/// </summary>
[Mapper]
public static partial class WmsMapper
{
    /// <summary>
    /// Menus 实体 → RoleMenuDTOs（平铺映射，忽略 FunctionButton）
    /// </summary>
    public static partial RoleMenuDTOs ToDto(this Menus menu);
}
