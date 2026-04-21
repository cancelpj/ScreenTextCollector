## 代码修改完以后使用如下命令编译项目验证修改
```powershell
dotnet build LabelTool/LabelTool.csproj -c Debug
```

## WinForms InitializeComponent() 正确写法要点
1. 代码流程 - 使用不带参数的构造函数创建全部控件对象 --> SuspendLayout() --> 设置控件属性、显式事件订阅 --> 添加到父容器中 --> ResumeLayout(false) + PerformLayout()
2. 不能使用匿名控件，所有控件都必须使用成员变量声明
3. 不能使用类常量，如: _zoomTrackBar.Minimum = (int)(MIN_ZOOM * 100)