# Auto7z_GUI

[English](#english) | [中文](#中文)

---

## English

### Attention

This project is now in LTS (Long-Term Support) mode. No further functional updates will be provided unless major bugs occur. Please head to **[Auto7z-WPF](https://github.com/Latte-Yogurt/Auto7z-WPF)** for the latest technical support.

### Index

- [Feature](#feature)
- [HowToUse](#howtouse)
- [Contribute](#contribute)
- [License](#license)

### Feature

- UI design compatible with Windows 10 style, offering a simple user experience.

### HowToUse

<div align="center">
  <img src="https://github.com/Latte-Yogurt/Auto7z_Rev/blob/main/Auto7z_Rev.png" width="300">
</div>

- The main interface is shown above.
- **Volume Size:** refers to the expected size of the volumes you want to generate for the compressed file. If the input file is larger than the specified size, it will automatically be split into multiple compressed volumes.
- **Generate Format:** refers to the format you want the compressed file to be in. Note: the operations for generating tar and tar.zst files do not support adding a password.
- **Add Password:** means to add a password to the generated compressed file, with the default being empty, indicating no password will be added.
- **Save config on exit:** means that the settings will be automatically saved upon exiting the program, and it is enabled by default.
- The usage methods are twofold: dragging and dropping files or folders into the window, and dragging and dropping files or folders onto the program icon.
- If you installed using the installer, you can right-click to open the context menu.

### Contribute

- Welcome to contribute! There is currently no contribution list.

### License

- The project is licensed under the [GPL-3.0 license](LICENSE).

---

## 中文

### 注意

本项目已进入 LTS 模式，如无影响使用的重大 bug 将不再进行功能性更新，移步至 **[Auto7z-WPF](https://github.com/Latte-Yogurt/Auto7z-WPF)** 获取最新项目的技术支持。

### 目录

- [特性](#特性)
- [如何使用](#如何使用)
- [贡献](#贡献)
- [许可证](#许可证)

### 特性

- 适配 Windows 10 的现代化 UI 风格，简单的操作体验。

### 如何使用

<div align="center">
  <img src="https://github.com/Latte-Yogurt/Auto7z_Rev/blob/main/Auto7z_Rev_CN.png" width="300" height="300">
</div>

- 主界面如上图所示。
- **分卷大小**的意思是创建压缩文件分卷时单个分卷的大小，当传入程序的文件体积大于设定的分卷大小时，则会按分卷压缩进行处理。
- **生成格式**的意思是你希望程序创建的压缩文件的格式，注意：当生成格式为 **tar** 和 **tar.zst** 时不支持 **添加密码** 选项。
- **添加密码**的意思是你希望添加到压缩文件的密码，默认为空，意味着默认不对压缩文件加密。
- **程序关闭时自动保存配置**的意思是在程序被主动关闭时自动将你设置的 **分卷大小**、**生成格式**、**添加密码** 选项的信息保存到配置文件中而无需手动点击保存配置。
- 使用方法有两种：拖拽文件到主程序窗口内和拖拽文件到主程序图标上。
- 如果你使用 Setup 安装程序进行安装则额外支持右键菜单选择 **Auto7z** 选项进行处理。

### 贡献

- 欢迎贡献！目前还没有贡献名单。

### 许可证

- 该项目的许可证为 [GPL-3.0 许可证](LICENSE)。
