using System;
using global::System.Collections.Generic;
using global::System.ComponentModel;

namespace Wms.Core.Domain.Enums
{
    public class EnumHelper
    {
        /// <summary>
        /// Enum 转 List
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<EnumEntity> EnumToList<T>()
        {
            List<EnumEntity> list = new List<EnumEntity>();

            foreach (var e in Enum.GetValues(typeof(T)))
            {
                EnumEntity m = new EnumEntity();
                object[] objArr = e.GetType().GetField(e.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), true);
                if (objArr != null && objArr.Length > 0)
                {
                    DescriptionAttribute da = objArr[0] as DescriptionAttribute;
                    m.Desction = da.Description;
                }
                m.EnumValue = Convert.ToInt32(e);
                m.EnumName = e.ToString();
                list.Add(m);
            }
            return list;
        }

        /// <summary>
        /// 获取枚举 Value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Name"></param>
        /// <returns></returns>
        public static int GetEnumValue<T>(string Name)
        {
            int result = 0;
            foreach (var e in Enum.GetValues(typeof(T)))
            {
                if (e.ToString() == Name)
                {
                    return Convert.ToInt32(e);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取枚举 Name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Name"></param>
        /// <returns></returns>
        public static string GetEnumName<T>(int Key)
        {
            string result = string.Empty;
            foreach (var e in Enum.GetValues(typeof(T)))
            {
                if (Convert.ToInt32(e) == Key)
                {
                    return e.ToString();
                }
            }
            return result;
        }

    }
}
