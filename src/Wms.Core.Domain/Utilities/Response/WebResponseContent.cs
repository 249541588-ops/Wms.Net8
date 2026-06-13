

using Wms.Core.Domain.Enums;

namespace Wms.Core.Domain.Utilities.Response
{
    /// <summary>
    /// 统一 api 接口返回对象
    /// </summary>
    public class WebResponseContent
    {
        /// <summary>
        /// 
        /// </summary>
        public WebResponseContent()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        public WebResponseContent(bool status)
        {
            this.Status = status;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool Status { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public object Data { get; set; }


        public static WebResponseContent Instance
        {
            get { return new WebResponseContent(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public WebResponseContent Succes(string message = null, object data = null)
        {
            this.Status = true;
            this.Code = Convert.ToInt32(ResponseType.OK).ToString();
            this.Message = message;
            this.Data = data;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseType"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public WebResponseContent Faile(ResponseType responseType, string message = null, object data = null)
        {
            this.Status = false;
            this.Code = Convert.ToInt32(responseType).ToString(); ;
            this.Message = message;
            this.Data = data;
            return this;
        }


    }
}
