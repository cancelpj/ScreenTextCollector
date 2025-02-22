using System;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using WalkingTec.Mvvm.Core;

namespace IoTGateway.Model;

[Comment("MQTT消息缓存")]
[Index(nameof(CreateTime))]
[Index(nameof(DeviceName))]
[Index(nameof(Result))]
public class MqttCache : BasePoco
{
    //public Device Device { get; set; }

    [Comment("缓存时间")]
    [Display(Name = "_Admin.CreateTime")]
    public new DateTime? CreateTime { get; set; }

    [Comment("所属设备")]
    [Display(Name = "DeviceName")]
    public string DeviceName { get; set; }

    [Comment("消息主题")]
    [Display(Name = "Topic")]
    public string Topic { get; set; }

    [Comment("消息体")]
    [Display(Name = "Payload")]
    public string Payload { get; set; }

    [Comment("MQTTnet 消息对象")]
    [Display(Name = "ApplicationMessage")]
    public string ApplicationMessage { get; set; }

    [Comment("推送结果(1-成功)")]
    [Display(Name = "Result")]
    public int Result { get; set; } = 0;

    [Comment("报错信息")]
    [Display(Name = "ErrorMessage")]
    public string ErrorMessage { get; set; }
}