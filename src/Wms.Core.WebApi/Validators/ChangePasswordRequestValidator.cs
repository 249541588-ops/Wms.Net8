using FluentValidation;
using Wms.Core.WebApi.Models;

namespace Wms.Core.WebApi.Validators;

/// <summary>
/// 修改密码请求验证器
/// </summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("旧密码不能为空");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新密码不能为空")
            .MinimumLength(6).WithMessage("新密码至少 6 位")
            .MaximumLength(100).WithMessage("新密码不能超过 100 个字符")
            .NotEqual(x => x.OldPassword).WithMessage("新密码不能与旧密码相同");
    }
}
