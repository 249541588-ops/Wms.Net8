using global::System.Reflection;

namespace Wms.Core.Domain.Attributes;

[System.AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AutoCopyToAttribute : Attribute
{
    /// <summary>
    /// 拷贝对象 src 原对象 dest 目标对象
    /// </summary>
    /// <param name="src">原对象</param>
    /// <param name="dest">目标对象</param>
    public static void CopyProps(object src, object dest)
    {
        ArgumentNullException.ThrowIfNull(src);
        ArgumentNullException.ThrowIfNull(dest);

        var sourceProps = src.GetType()
            .GetProperties()
            .Where(x => x.IsDefined(typeof(AutoCopyToAttribute)))
            .ToArray();
        foreach (var srcProp in sourceProps)
        {
            var target = dest.GetType()
                .GetProperty(srcProp.Name, BindingFlags.Public | BindingFlags.Instance);
            if (target == null)
            {
                throw new InvalidOperationException($"在目标类型中找不到同名属性。目标类型【{dest.GetType()}】，属性名【{srcProp.Name}】。");
            }
            object val = srcProp.GetValue(src);
            target.SetValue(dest, val);
        }
    }
}
