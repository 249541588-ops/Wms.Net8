using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wms.Core.Application.DTOs
{
    /// <summary>
    /// 创建菜单请求
    /// </summary>
    public record CreateMenuRequest
    {
        /// <summary>
        ///
        /// </summary>
        public int ParentId { get; init; }

        /// <summary>
        ///
        /// </summary>
        public int Sort { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? EnglishName { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? GermanName { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Url { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? ImgUrl { get; init; }

        /// <summary>
        /// 助记码
        /// </summary>
        public int IsDisplay { get; init; } = 0;

        /// <summary>
        /// 批次管理
        /// </summary>
        public string? FunctionButton { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Creator { get; init; }
    }

    /// <summary>
    /// 更新菜单请求
    /// </summary>
    public record UpdateMenuRequest
    {
        /// <summary>
        ///
        /// </summary>
        public int ParentId { get; init; }

        /// <summary>
        ///
        /// </summary>
        public int Sort { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? EnglishName { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? GermanName { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Url { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? ImgUrl { get; init; }

        /// <summary>
        /// 助记码
        /// </summary>
        public int? IsDisplay { get; init; }

        /// <summary>
        /// 批次管理
        /// </summary>
        public string? FunctionButton { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string? Editor { get; init; }
    }


    public record RoleMenuDTOs
    {
        /// <summary>
        /// 菜单ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int ParentId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int Sort { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? EnglishName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? GermanName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? ImgUrl { get; set; }

        /// <summary>
        /// 菜单功能按钮
        /// </summary>
        public string FunBtns { get; set; } = string.Empty;

        /// <summary>
        ///
        /// </summary>
        public List<RoleMenuDTOs> Child { get; set; } = new List<RoleMenuDTOs>();
    }

    /// <summary>
    /// 批量删除请求
    /// </summary>
    public record BatchDeleteRequest
    {
        /// <summary>
        /// 要删除的 ID 列表
        /// </summary>
        public List<int> Ids { get; init; } = new List<int>();
    }

}
