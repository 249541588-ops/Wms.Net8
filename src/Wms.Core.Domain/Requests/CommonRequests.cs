namespace Wms.Core.Domain.Requests;

/// <summary>
/// 设置启用/禁用请求
/// </summary>
public class SetEnabledRequest
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 修改用户
    /// </summary>
    public string? ModifiedBy { get; set; }
}

// Note: CreateUserRequest, UpdateUserRequest, SetUserEnabledRequest, ChangePasswordRequest
// are defined in Wms.Core.Application.DTOs.UserDtos.cs and Wms.Core.WebApi.Models.AuthModels.cs
// LoginRequest, RefreshTokenRequest are in Wms.Core.WebApi.Models.AuthModels.cs
// BatchDeleteRequest, CreateMenuRequest, UpdateMenuRequest, RoleMenuDTOs are in Wms.Core.Application.DTOs.MenusDtos.cs
// CreateSys_LanguageRequest, UpdateSys_LanguageRequest are in Wms.Core.Application.DTOs.Sys_LanguageDtos.cs
