using FluentValidation;
using Wms.Core.Application.DTOs;

namespace Wms.Core.WebApi.Validators;

/// <summary>
/// WCS 请求验证器
/// </summary>
public class WcsRequestValidator : AbstractValidator<WcsRequest>
{
    public WcsRequestValidator()
    {
        RuleFor(x => x.ContainerCode)
            .NotNull().WithMessage("容器编码不能为 null")
            .Must(codes => codes.Length > 0).WithMessage("至少需要一个容器编码");

        RuleForEach(x => x.ContainerCode)
            .NotEmpty().WithMessage("容器编码不能为空字符串");
    }
}
