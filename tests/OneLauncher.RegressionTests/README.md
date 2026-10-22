# 离线回归检查

该项目为可独立运行的检查程序，不属于主解决方案，不使用真实用户配置，不调用启动初始化或在线服务。

```powershell
dotnet restore tests/OneLauncher.RegressionTests/OneLauncher.RegressionTests.csproj
dotnet build tests/OneLauncher.RegressionTests/OneLauncher.RegressionTests.csproj --no-restore
dotnet tests/OneLauncher.RegressionTests/bin/Debug/net10.0/OneLauncher.RegressionTests.dll artifacts/settings-regression
```

最后一个参数为可选输出目录，默认 `artifacts/settings-regression`。测试配置保存在 `data/<unique-id>`，并输出三张 Headless + Skia 运行时渲染截图。程序遇到断言失败会以非零状态退出。

覆盖版本迁移、损坏文件保护、失败重试、并发版本登记、排序持久化、全局管理器保存、安装前目录校验、设置筛选、导航编辑及保存、主窗口可见性和启动 DI 依赖图。

测试通过反射访问内部 ViewModel 和迁移入口，以避免给生产代码增加公开测试接口；如果重命名这些内部类型，需要同步更新检查程序。完整启动图使用 `ValidateOnBuild`，实际实例化全部已注册工厂及六个无需网络的页面；主页和资源检索页的构造函数会启动在线任务，因此只验证其依赖图。

当前文件共享锁故障模拟针对 Windows 环境。没有覆盖真实下载、登录、联机和 Native AOT 发布。
