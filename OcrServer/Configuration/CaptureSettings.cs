namespace OcrServer.Configuration;

/// <summary>
/// 屏幕采集配置（从 CaptureSettings.{DeviceCode}.json 加载）
/// 基于 PluginInterface.CaptureSettings 精简而来（去掉了 OCR 图像预处理参数）
/// </summary>
public class CaptureSettings
{
    /// <summary>
    /// OCR 引擎类型（目前仅支持 PaddleOCR）
    /// </summary>
    public string OcrEngine { get; set; } = "PaddleOCR";

    /// <summary>
    /// 图像验证区域列表（用于判断目标画面是否存在）
    /// </summary>
    public List<ImageVerificationArea> VerificationAreas { get; set; } = new();

    /// <summary>
    /// 图像采集区域列表
    /// </summary>
    public List<ImageCollectionArea> CollectionAreas { get; set; } = new();
}

/// <summary>
/// 图像检测区域配置项（用于验证目标画面是否存在）
/// </summary>
public class ImageVerificationArea
{
    /// <summary>
    /// 所属屏幕编号，从 0 开始
    /// </summary>
    public int ScreenNumber { get; set; }

    /// <summary>
    /// 区域左上角 X 坐标
    /// </summary>
    public int TopLeftX { get; set; }

    /// <summary>
    /// 区域左上角 Y 坐标
    /// </summary>
    public int TopLeftY { get; set; }

    /// <summary>
    /// 区域宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 区域高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 检测图片的文件名（位于 data/ 目录下）
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// 像素级相似度匹配阈值（0.0-1.0）
    /// </summary>
    public float MatchThreshold { get; set; } = 0.95f;
}

/// <summary>
/// 图像采集区域配置项
/// </summary>
public class ImageCollectionArea
{
    /// <summary>
    /// 所属屏幕编号，从 0 开始
    /// </summary>
    public int ScreenNumber { get; set; }

    /// <summary>
    /// 采集项名称（如 V001、ALARM001），也是 OCR 结果的 Key
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// MQTT Topic 名称（必填，必须与 appsettings.json 中对应设备的 Topics[n].Name 一致）
    /// </summary>
    public string Topic { get; set; } = "";

    /// <summary>
    /// 区域左上角 X 坐标
    /// </summary>
    public int TopLeftX { get; set; }

    /// <summary>
    /// 区域左上角 Y 坐标
    /// </summary>
    public int TopLeftY { get; set; }

    /// <summary>
    /// 区域宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 区域高度
    /// </summary>
    public int Height { get; set; }
}
