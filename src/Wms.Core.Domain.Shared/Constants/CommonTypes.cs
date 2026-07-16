namespace Wms.Core.Domain.Constants
{
    /// <summary>
    /// 
    /// </summary>
    public static class CommonTypes
    {

        /// <summary>
        /// 托盘码前缀_化成
        /// </summary>
        public const string 托盘码前缀_化成 = "HC";

        /// <summary>
        /// 托盘码前缀_分容
        /// </summary>
        public const string 托盘码前缀_分容 = "FR";

        /// <summary>
        /// 空托盘物料
        /// </summary>
        public const string 空托盘 = "M999999999999";

        /// <summary>
        /// 工装板物料
        /// </summary>
        public const string 工装板 = "M888888888888";

        /// <summary>
        /// 托盘码空
        /// </summary>
        public const string 托盘码空 = "0000000000";

        /// <summary>
        /// 电芯码空
        /// </summary>
        public const string 电芯码空 = "000000000000000000000000";

        /// <summary>
        /// 高温浸润
        /// </summary>
        public const string 高温浸润时间 = "GAOWENTIME";

        /// <summary>
        /// 恒温静置一天
        /// </summary>
        public const string 恒温静置一天时间 = "GAOWENLAOHUATIME";

        /// <summary>
        /// 恒温三天
        /// </summary>
        public const string 常温一天时间 = "SANTIANTIME";

        /// <summary>
        /// 恒温七天
        /// </summary>
        public const string 常温七天时间 = "SEVENDAYSTIME";

        /// <summary>
        /// 成品库
        /// </summary>
        public const string 成品库时间 = "CHENGPINTIME";

        /// <summary>
        /// 三天库出库档位
        /// </summary>
        public const string 三天库出库档位 = "THREEDAYSBATCH";

        /// <summary>
        /// 化成出库批次(高温浸润出库批次)
        /// </summary>
        public const string 高温浸润出库批次 = "HUACHENGBATCH";

        /// <summary>
        /// 分容出库批次（高温静置出库批次）
        /// </summary>
        public const string 高温静置出库批次 = "FENRONGBATCH";

        /// <summary>
        /// 一天出库批次
        /// </summary>
        public const string 一天库出库批次 = "ONEDAYSBATCH";

        /// <summary>
        /// 十五天出库批次
        /// </summary>
        public const string 十五天库出库批次 = "SEVENDAYSBATCH";

        /// <summary>
        /// 高温浸润入库批次（104）
        /// </summary>
        public const string 高温浸润入库批次 = "GWJRINBOUNDBATCH";
        /// <summary>
        /// 1号七天库入库批次
        /// </summary>
        public const string 七天库入库批次1号 = "SEVENDAYINBOUNDBATCH1";

        /// <summary>
        /// 2号七天库入库批次
        /// </summary>
        public const string 七天库入库批次2号 = "SEVENDAYINBOUNDBATCH2";

        /// <summary>
        /// 化成排废通道
        /// </summary>
        public const string 化成排废通道 = "HUACHENGDISCHARGE";

        /// <summary>
        /// 化成排废通道
        /// </summary>
        public const string 化成排废批次 = "HUACHENGDISCHARGEBATCH";

        /// <summary>
        /// 记录化成排废工位一上托盘码
        /// </summary>
        public const string 化成排废工位一托盘码 = "HCPFGW1TPM";

        /// <summary>
        /// 记录化成排废工位二上托盘码
        /// </summary>
        public const string 化成排废工位二托盘码 = "HCPFGW2TPM";

        /// <summary>
        /// 化成排废重组自动装盘假电芯档位
        /// </summary>
        public const string 化成排废重组自动装盘假电芯档位 = "HCPFCZZDZPJDXDW";

        /// <summary>
        /// 一注截批人工装盘假电芯档位
        /// </summary>
        public const string 一注截批人工装盘假电芯档位 = "YZJPRKZPJDXDW";

        /// <summary>
        /// OCV3排废通道
        /// </summary>
        public const string OCV3排废通道 = "OCV3DISCHARGE";

        /// <summary>
        /// DCIR排废通道
        /// </summary>
        public const string DCIR排废通道 = "DCIRDISCHARGE";

        /// <summary>
        /// 包胶分档映射
        /// </summary>
        public const string 包胶分档映射 = "BJFDDISCHARGE";

        /// <summary>
        /// 包胶分档档位映射
        /// </summary>
        public const string 包胶分档档位映射 = "BJFDDISCHARGEFL";

        /// <summary>
        /// 扫码NG
        /// </summary>
        public const string 扫码NG = "saomang";

        /// <summary>
        /// 绝缘NG
        /// </summary>
        public const string 绝缘NG = "insulation";

        /// <summary>
        /// 厚度NG
        /// </summary>
        public const string 厚度NG = "thickness";

        /// <summary>
        /// 重量NG
        /// </summary>
        public const string 重量NG = "weight";

        /// <summary>
        /// 外观NG
        /// </summary>
        public const string 外观NG = "appearance";

        #region 获取巨一数值Key
        public const string 重量 = "重量";

        public const string 绝缘测试值 = "绝缘测试值";

        public const string 测厚夹紧压力 = "测厚夹紧压力";

        public const string 厚度值 = "厚度值";

        public const string 耐压值 = "耐压值";
        #endregion

        /// <summary>
        /// A档(包胶分档映射)
        /// </summary>
        public const string A档 = "A";

        /// <summary>
        /// B1档(包胶分档映射)
        /// </summary>
        public const string B1档 = "B1";

        /// <summary>
        /// B2档(包胶分档映射)
        /// </summary>
        public const string B2档 = "B2";

        /// <summary>
        /// B3档(包胶分档映射)
        /// </summary>
        public const string B3档 = "B3";

        /// <summary>
        /// B4档(包胶分档映射)
        /// </summary>
        public const string B4档 = "B4";

        /// <summary>
        /// C档(包胶分档映射)
        /// </summary>
        public const string C档 = "C";

        /// <summary>
        /// 验证电芯K值
        /// </summary>
        public const string 验证电芯K值 = "VerifyKValue";
        
        /// <summary>
        /// 验证电芯Marking值
        /// </summary>
        public const string 验证电芯Marking值 = "VerifyMarkingValue";
        
        /// <summary>
        /// 验证电芯分拣
        /// </summary>
        public const string 验证电芯分拣 = "VerifyPickValue";

        /// <summary>
        /// 假电芯前缀格式
        /// </summary>
        public const string 假电芯前缀格式 = "FakeBatteryPrefix";

        /// <summary>
        /// 托盘条码格式
        /// </summary>
        public const string 托盘条码格式 = "TrayCodeFormat";

        /// <summary>
        /// 托盘条码长度
        /// </summary>
        public const string 托盘条码长度 = "TrayCodeLength";

        /// <summary>
        /// 托盘条码前缀格式
        /// </summary>
        public const string 托盘条码前缀格式 = "TrayCodePrefix";

        /// <summary>
        /// OCV3自动装盘批次
        /// </summary>
        public const string OCV3自动装盘批次 = "OCV3ZDZPBATCH";

        /// <summary>
        /// OCV3机械手状态 0停用 1启用
        /// </summary>
        public const string OCV3机械手状态 = "OCV3DISCHARGESTATE";

        /// <summary>
        /// 化成二次工艺档位
        /// </summary>
        public const string 化成二次工艺档位 = "HUACHENGSECONDARY";

        /// <summary>
        /// OCV3二次工艺档位
        /// </summary>
        public const string OCV3二次工艺档位 = "OCV3SECONDARY";

        /// <summary>
        /// DCIR二次工艺档位
        /// </summary>
        public const string DCIR二次工艺档位 = "DCIRSECONDARY";

        /// <summary>
        /// 高温浸润库对应巷道
        /// </summary>
        public const string 高温浸润库对应巷道 = "L1";

        /// <summary>
        /// 高温静置库对应巷道
        /// </summary>
        public const string 高温静置库对应巷道 = "L6";

        /// <summary>
        /// 一天库对应巷道
        /// </summary>
        public const string 一天库对应巷道 = "L7";

        /// <summary>
        /// 七天库对应巷道
        /// </summary>
        public const string 七天库对应巷道 = "'L8','L9'";

        /// <summary>
        /// 七天一库对应巷道
        /// </summary>
        public const string 七天一库对应巷道 = "L8";

        /// <summary>
        /// 七天二库对应巷道
        /// </summary>
        public const string 七天二库对应巷道 = "L9";

        /// <summary>
        /// 看板一对应巷道
        /// </summary>
        public const string 看板一对应巷道 = "L1,L2,L3";

        /// <summary>
        /// 上架工艺对应库区
        /// </summary>
        public static string[] 拆盘对应库区 = new string[] { "L5", "L6", "L7", "L8", "L9", "L10" };

        /// <summary>
        /// 上架工艺对应库区
        /// </summary>
        public static string[] 上架工艺对应库区 = new string[] { "L1", "L2", "L3", "L7", "L8", "L9", "L10" };

        /// <summary>
        /// 上架工艺对应上架口
        /// </summary>
        public static string[] 上架工艺对应上架口 = new string[] { "4557", "4547", "4537", "4528" };

        /// <summary>
        /// 化成分容柜对应库区
        /// </summary>
        public static string[] 化成分容柜对应库区 = new string[] { "L1", "L4", "L5", "L6" };

        /// <summary>
        /// 倒序对应库区
        /// </summary>
        public static string[] 倒序对应库区 = new string[] { "4557","4547", "4537", "4528" };

        /// <summary>
        /// 倒序对应库区
        /// </summary>
        public static string[] 双叉对应库区 = new string[] { "L1", "L2", "L3", "L4", "L5", "L6" };

        /// <summary>
        /// 工装板叉2位置
        /// </summary>
        public static string[] 工装板叉2位置 = new string[] { "HC1082801", "HC1082802", "HC1082803" };

        /// <summary>
        /// 异常电芯批次
        /// </summary>
        public const string 异常电芯批次 = "000000";
       
        /// <summary>
        /// 账号配置
        /// </summary>
        public const string 账号配置 = "AccountConfig";

        /// <summary>
        /// 系统账号
        /// </summary>
        public const string 系统账号 = "admin";

        /// <summary>
        /// 任务标识
        /// </summary>
        public const string 任务标识 = "QUANTITU_";

        #region 上传Mes时任务类型标识
        /// <summary>
        /// 入库
        /// </summary>
        public const string 入库 = "入库";

        /// <summary>
        /// 出库
        /// </summary>
        public const string 出库 = "出库";

        /// <summary>
        /// 移库
        /// </summary>
        public const string 移库 = "移库";
        #endregion

        /// <summary>
        /// 清洗空托呼叫位置
        /// </summary>
        public const string 清洗空托呼叫位置 = "7777";

    }
}