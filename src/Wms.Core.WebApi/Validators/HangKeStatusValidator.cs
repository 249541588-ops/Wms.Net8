using FluentValidation;
using Wms.Core.WebApi.Models;

namespace Wms.Core.WebApi.Validators;

/// <summary>
/// 杭可货位状态变更请求验证器
/// </summary>
public class HangKeStatusValidator : AbstractValidator<HangKeStatus>
{
    /// <summary>
    /// 初始化杭可货位状态变更请求验证器
    /// </summary>
    public HangKeStatusValidator()
    {
        RuleFor(x => x.LocationCode)
            .NotEmpty().WithMessage("货位不能空")
            .MaximumLength(16).WithMessage("货位编码长度不能超过 16");

        RuleFor(x => x.HKState)
            .InclusiveBetween(1, 8).WithMessage("状态 {PropertyValue} 非法，应为 1-8");
    }
}
