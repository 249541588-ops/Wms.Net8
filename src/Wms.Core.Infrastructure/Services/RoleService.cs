using Microsoft.Extensions.Logging;
using Wms.Core.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Wms.Core.Infrastructure.Persistence;
using Wms.Core.Domain.Requests;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Repositories;
using Wms.Core.Application.Ports;

namespace Wms.Core.Infrastructure.Services;

public class RoleService : IRoleService
{

    private readonly WmsDbContext _db;   
    private readonly ILogger<RoleService> _logger;
    private readonly IRepository<Role,int> _repository;

    public RoleService(        
        WmsDbContext db,
        IRepository<Role, int> repository,
    ILogger<RoleService> logger)
    {        
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 初始化 admin 角色按钮权限
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<bool> InitialConfiguration()
    {
        // 验证参数
        Role role = await _db.Set<Role>().Where(x => x.Id == 1).FirstOrDefaultAsync();
        if (role == null)
            throw new InvalidRequestException("记录不存在");

        var menus = _db.Set<Menus>();
        if (role == null)
            throw new InvalidRequestException("记录不存在");

        string[] arrary = new string[] { "Add", "Edit", "Delete", "Search", "Reset", "BatchDelete", "ResetPassword", "ChangeStatus", "ChangeLock" };
        foreach (var menu in menus) {
            foreach (var item in arrary) {
                Role_Menu_Funs funs = new Role_Menu_Funs()
                {
                     MenuId = menu.Id,
                      RoleId = role.Id,
                      FunctionButton = item,
                };
                _db.Add(funs);
            }
        }
        return true;
    }

    /// <summary>
    /// 设置角色菜单按钮权限（由外层 TransactionMiddleware 管理事务）
    /// </summary>
    /// <param name="Roleid">角色 ID</param>
    /// <param name="request">菜单权限列表</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<bool> SettingRoleMenus(int Roleid, List<SettingRoleMenusRequest> request)
    {
        // 验证参数
        Role role = await _db.Set<Role>().Where(x => x.Id == Roleid).FirstOrDefaultAsync();
        if (role == null)
            throw new InvalidRequestException($"角色 ID {Roleid} 不存在");

        if (request == null || request.Count == 0)
            throw new InvalidRequestException("请选择菜单权限");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 步骤1：删除旧的按钮权限
            var oldButtonFuns = _db.Set<Role_Menu_Funs>()
                .Where(x => x.RoleId == Roleid)
                .ToList();

            foreach (var fun in oldButtonFuns)
            {
                _db.Remove(fun);
            }

            // 步骤2：删除旧的菜单关联
            var oldRoleMenus = _db.Set<Role_Menu>()
                .Where(x => x.RoleId == Roleid)
                .ToList();

            foreach (var rm in oldRoleMenus)
            {
                _db.Remove(rm);
            }

            // 步骤3：立即执行删除操作
            await _db.SaveChangesAsync();

            // 步骤4：添加新的菜单和按钮权限
            foreach (var menu in request)
            {
                // 创建菜单关联
                Role_Menu role_Menu = new Role_Menu()
                {
                    MenuId = menu.MenuId,
                    RoleId = role.Id,
                };
                _db.Add(role_Menu);

                // 创建按钮权限
                foreach (string item in menu.Btn ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        Role_Menu_Funs funs = new Role_Menu_Funs()
                        {
                            MenuId = menu.MenuId,
                            RoleId = role.Id,
                            FunctionButton = item.Trim(),
                        };
                        _db.Add(funs);
                    }
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("角色 {RoleId} 菜单权限设置成功，共 {Count} 个菜单", Roleid, request.Count);
        return true;
    }
}
