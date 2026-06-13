using System;
using global::System.IO;
using global::System.Net;
using global::System.Text;
using global::System.Text.Json;

namespace Wms.Core.Domain.Utilities.Response
{
    /// <summary>
    /// Wcs 结果集
    /// </summary>
    public class WcsResult
    {
        /// <summary>
        /// 
        /// </summary>
        public WcsResult()
        {
            success = true;
            currentoperation = 0;
        }

        /// <summary>
        /// 成功标识
        /// </summary>
        public bool success { get; set; }
        /// <summary>
        /// 查询状态码 备用
        /// </summary>
        public string? code { get; set; }
        /// <summary>
        /// 提示信息
        /// </summary>
        public string? msg { get; set; }
        /// <summary>
        /// 结果编码
        /// </summary>
        public string? resultcode { get; set; }
        /// <summary>
        /// 业务流程
        /// </summary>
        public int? currentoperation { get; set; }
        /// <summary>
        /// 数据集合
        /// </summary>
        public object? data { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ApiResultMes
    {
        /// <summary>
        /// 成功标识
        /// </summary>
        public bool status { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? result { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? msg { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? message { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public object? data { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? traceID { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? errorMsg { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ApiResultHelper
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static ApiResultMes PostMes(string url, string data, string token = "")
        {
            var request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Timeout = 30 * 1000; //����30s�ĳ�ʱ
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2272.118 Safari/537.36";
            //request.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
            request.ContentType = "application/json";
            request.Method = "POST";

            if (token.Length > 0)
            {
                request.Headers.Add("Authorization", "Bearer " + token);
            }

            byte[] data2 = Encoding.UTF8.GetBytes(data);
            request.ContentLength = data2.Length;
            Stream postStream = request.GetRequestStream();
            postStream.Write(data2, 0, data2.Length);
            postStream.Close();

            ApiResultMes apiResult = new ApiResultMes();

            try
            {
                using (var res = request.GetResponse() as HttpWebResponse)
                {
                    StreamReader reader = new StreamReader(res.GetResponseStream(), Encoding.UTF8);
                    string result = reader.ReadToEnd();
                    apiResult = JsonSerializer.Deserialize<ApiResultMes>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("401"))
                {
                    apiResult.code = "401";
                }
                apiResult.status = false;
                apiResult.message = ex.Message;
            }

            return apiResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="resultcode"></param>
        /// <param name="currentoperation"></param>
        /// <param name="code"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static WcsResult WcsSuccess(string msg, string resultcode, int currentoperation, string code = "", object data = null)
        {
            WcsResult result = new WcsResult()
            {
                success = true,
                msg = msg,
                resultcode = resultcode,
                currentoperation = currentoperation,
                code = code,
                data = data,
            };
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="resultcode"></param>
        /// <param name="currentoperation"></param>
        /// <param name="code"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static WcsResult WcsFail(string msg, string resultcode, int currentoperation, string code = "", object data = null)
        {
            WcsResult result = new WcsResult()
            {
                success = false,
                msg = msg,
                resultcode = resultcode,
                currentoperation = currentoperation,
                code = code,
                data = data,
            };
            return result;
        }
    }
}