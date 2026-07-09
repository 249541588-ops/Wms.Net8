using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Core.Application.DTOs;
using Wms.Core.Infrastructure.Mappers;
using Wms.Core.Domain.Entities;
using Wms.Core.Domain.Entities.Identity;
using Wms.Core.Domain.Enums;
using Wms.Core.Domain.Repositories;
using Wms.Core.Domain.Common;
using Wms.Core.WebApi.Extensions;
using Wms.Core.WebApi.Filters;
using System.Linq;

namespace Wms.Core.WebApi.Controllers;

/// <summary>
/// 菜单管理 API 控制器
/// </summary>
[ApiController]
[ApiVersion("1.0")]
//[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize]
public partial class MenusController : ControllerBase
{
    private readonly IRepository<Role_Menu_Funs, long> _repositoryFuns;
    private readonly IRepository<Menus, int> _repository;
    //private readonly IStockRepository _stockRepository;
    private readonly ILogger<MenusController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MenusController(
        IRepository<Role_Menu_Funs, long> repositoryFuns,
        IRepository<Menus, int> repository,
        //IStockRepository stockRepository,
        ILogger<MenusController> logger)
    {
        _repositoryFuns = repositoryFuns ?? throw new ArgumentNullException(nameof(repositoryFuns));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        //_stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 根据角色名称获取菜单列表（分页）
    /// </summary>
    [HttpGet("ByRolePaged")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetMenusByRoleNamePaged(
        string? roleName = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            // SQL 查询语句
            var sql = @"
            SELECT Id, ParentId, Sort, Name, EnglishName, GermanName, Url, ImgUrl, IsDisplay, FunctionButton, Creator, Editor, CreateTime, EditTime FROM Menus
            WHERE Id IN (
                SELECT MenuId FROM Role_Menu
                WHERE RoleId = (
                    SELECT Id FROM Roles
                    WHERE RoleName = @roleName
                )
            )
        ";

            // 统计总数的 SQL
            var countSql = @"
            SELECT COUNT(*) FROM Menus
            WHERE Id IN (
                SELECT MenuId FROM Role_Menu
                WHERE RoleId = (
                    SELECT Id FROM Roles
                    WHERE RoleName = @roleName
                )
            )
        ";

            // 参数
            var parameters = new Dictionary<string, object>
        {
            { "roleName", roleName ?? string.Empty }
        };

            // 执行分页查询
            var (data, totalCount, totalPages) = _repository.ExecuteSqlPaged(
                sql: sql,
                countSql: countSql,
                parameters: parameters,
                pageNumber: pageNumber,
                pageSize: pageSize,
                orderBy: "Sort ASC, Id ASC"  // 按排序字段和 ID 排序
            );

            // 使用 PagedResult 模型
            var pagedResult = PagedResult.Create(
                data: data,
                pageNumber: pageNumber,
                pageSize: pageSize,
                totalCount: totalCount
            );

            return Result<object>.Success(pagedResult, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }


    /// <summary>
    /// 根据角色获取菜单
    /// </summary>
    /// <param name="roleName">角色（可选）</param>
    /// <returns>数据列表</returns>
    [HttpGet("ByRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetListByRoleName(string? roleName = null)
    {
        try
        {
            // 使用参数化查询，防止 SQL 注入

            var funSql = "SELECT Id, MenuId, RoleId, FunctionButton FROM Role_Menu_Funs WHERE RoleId = (SELECT Id FROM Roles WHERE RoleName = @roleName)";
            var funParameters = new Dictionary<string, object>
            {
                { "roleName", roleName ?? string.Empty }
            };
            var funQuery = _repositoryFuns.ExecuteSql(funSql, funParameters);


            var menuSql = "SELECT Id, ParentId, Sort, Name, EnglishName, GermanName, Url, ImgUrl, IsDisplay, FunctionButton, Creator, Editor, CreateTime, EditTime FROM Menus WHERE Id IN (SELECT MenuId FROM Role_Menu WHERE RoleId = (SELECT Id FROM Roles WHERE RoleName = @roleName))";
            var menuParameters = new Dictionary<string, object>
            {
                { "roleName", roleName ?? string.Empty }
            };
            var query = _repository.ExecuteSql(menuSql, menuParameters);

            List<RoleMenuDTOs> pListDTOs = new List<RoleMenuDTOs>();
            if (query != null)
            {
                var PMenus = query.Where(x => x.ParentId == 0 && x.IsDisplay == 0).OrderBy(x => x.Sort);
                foreach (Menus pMenu in PMenus)
                {

                    var menuDTOs = pMenu.ToDto();

                    List<RoleMenuDTOs> cListDTOs = new List<RoleMenuDTOs>();
                    var CMenus = query.Where(x => x.ParentId == pMenu.Id && x.IsDisplay == 0).OrderBy(x => x.Sort);

                    foreach (Menus cMenu in CMenus)
                    {
                        string funStrings = string.Empty;
                        if (funQuery != null)
                        {
                            var funLists = funQuery.Where(x => x.MenuId == cMenu.Id);
                            if(funLists!=null)
                            funStrings = string.Join(",", funLists.Select(x=>x.FunctionButton));
                        }

                        var cDTOs = cMenu.ToDto() with { FunBtns = funStrings };
                        menuDTOs.Child.Add(cDTOs);
                    }

                    pListDTOs.Add(menuDTOs);
                }
            }

            return Result<object>.Success(pListDTOs, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }


    /// <summary>
    /// 获取所有菜单
    /// </summary>
    /// <param name="keyword">搜索关键字（可选）</param>
    /// <param name="parentId">搜索父级ID（可选）</param>
    /// <param name="enabled">是否启用（可选）</param>
    /// <param name="pageNumber">页码（默认 1）</param>
    /// <param name="pageSize">每页大小（默认 20，最大 100）</param>
    /// <returns>数据列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Result GetAll(string? keyword = null, string? parentId = null, bool? enabled = null, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            // 限制每页最大大小
            pageSize = Math.Min(pageSize, 100);

            var query = _repository.GetAll();

            // 关键字搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.Name.Contains(keyword));
            }

            if (!string.IsNullOrEmpty(parentId))
            {
                query = query.Where(m => m.ParentId == Convert.ToInt32(parentId));
            }

            // 按启用状态筛选
            if (enabled.HasValue)
            {
                query = query.Where(m => m.IsDisplay == (enabled.Value ? 0 : 1));
            }

            // 获取总数
            var totalCount = query.Count();

            // 分页
            var lists = query
                .OrderBy(m => m.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();


            var pagedResponse = new PagedResult<Menus>
            {
                Data = lists,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result<PagedResult<Menus>>.Success(pagedResponse, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取列表失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 根据 ID 获取对象
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>对象详情</returns>
    [HttpGet("{id:int}")]
    //[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result GetById(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            return Result<Menus>.Success(model, "获取成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对象失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 创建新对象
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的对象</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Result Create([FromBody] CreateMenuRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Result.Fail("请求参数无效");
            }

            // 验证名称是否唯一
            if (_repository.Exists(m => m.Name == request.Name))
            {
                return Result.Fail("名称已存在");
            }

            var model = new Menus
            {
                ParentId = request.ParentId,
                Sort = request.Sort,
                Name = request.Name,
                EnglishName = request.EnglishName,
                GermanName = request.GermanName,
                Url = request.Url,
                ImgUrl = request.ImgUrl,
                IsDisplay = request.IsDisplay,
                FunctionButton = request.FunctionButton,
                Creator = request.Creator,
                CreateTime = DateTime.Now,
            };

            var createdModel = _repository.Add(model);

            return Result<Menus>.Success(createdModel, "创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 更新对象
    /// </summary>
    /// <param name="id">对象 ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新后的对象</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Result Update(int id, [FromBody] UpdateMenuRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Result.Fail("请求参数无效");
            }

            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 更新字段
            model.ParentId = request.ParentId;
            model.Sort = request.Sort;
            model.Name = request.Name ?? model.Name;
            model.EnglishName = request.EnglishName ?? model.EnglishName;
            model.GermanName = request.GermanName ?? model.GermanName;
            model.Url = request.Url ?? model.Url;
            model.ImgUrl = request.ImgUrl ?? model.ImgUrl;
            model.IsDisplay = request.IsDisplay ?? model.IsDisplay;
            model.FunctionButton = request.FunctionButton;
            model.Editor = request.Editor;
            model.EditTime = DateTime.Now;

            _repository.Update(model);

            return Result<Menus>.Success(model, "更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 删除物料
    /// </summary>
    /// <param name="id">物料 ID</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:int}")]
    //[Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result Delete(int id)
    {
        try
        {
            var model = _repository.GetById(id);
            if (model == null)
            {
                return Result.Fail("记录不存在", "404");
            }

            // 先删除 Role_Menu 表中的关联记录
            // var deleteRoleMenuSql = "DELETE FROM Role_Menu WHERE MenuId = :menuId";
            // _repository.ExecuteSql(deleteRoleMenuSql, new Dictionary<string, object>
            // {
            //     { "menuId", id }
            // });

            // 然后删除菜单
            _repository.Delete(id);

            return Result.Success("删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }

    /// <summary>
    /// 批量删除菜单
    /// </summary>
    /// <param name="request">批量删除请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("BatchDelete")]
    //[Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Result BatchDelete([FromBody] BatchDeleteRequest request)
    {
        try
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return Result.Fail("请选择要删除的记录");
            }

            int successCount = 0;
            int failCount = 0;
            List<string> errors = new List<string>();

            foreach (int id in request.Ids)
            {
                try
                {
                    var model = _repository.GetById(id);
                    if (model != null)
                    {
                        // 先删除 Role_Menu 表中的关联记录
                        // var deleteRoleMenuSql = "DELETE FROM Role_Menu WHERE MenuId = :menuId";
                        // _repository.ExecuteSql(deleteRoleMenuSql, new Dictionary<string, object>
                        // {
                        //     { "menuId", id }
                        // });

                        // 然后删除菜单
                        _repository.Delete(id);
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        errors.Add($"ID {id} 不存在");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    errors.Add($"ID {id} 删除失败: {ex.Message}");
                }
            }

            string message = $"批量删除完成: 成功 {successCount} 个";
            if (failCount > 0)
            {
                message += $", 失败 {failCount} 个";
                if (errors.Any())
                {
                    message += $". {string.Join("; ", errors.Take(3))}";
                    if (errors.Count > 3)
                    {
                        message += "...";
                    }
                }
            }

            if (failCount == 0)
            {
                return Result.Success(message);
            }
            else if (successCount > 0)
            {
                return Result.Success(message);
            }
            else
            {
                return Result.Fail(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除失败: {Message}", ex.Message);
            return Result.Fail("操作失败");
        }
    }


}
