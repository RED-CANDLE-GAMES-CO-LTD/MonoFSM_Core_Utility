(Gen by Claude Sonnet 4)
# MonoFSM Core Utility
**Unity Package Management & Git Dependency Installer**
[中文版本](#中文版本) | [English Version](#english-version)

---

## English Version

### Overview
MonoFSM Core Utility is a comprehensive Unity editor tool that provides advanced package management and Git dependency installation capabilities. This tool streamlines the process of managing Unity packages and their dependencies through an intuitive editor interface.

### Key Features

#### 🔧 Git Dependency Manager
- **Visual Package Management**: Easy-to-use editor window for managing Git-based Unity packages
- **Automatic Dependency Resolution**: Intelligent analysis and installation of required dependencies
- **Assembly Analysis**: Advanced assembly dependency analysis with automatic package.json updates
- **Batch Operations**: Install multiple packages and dependencies in one go

#### 🛠 Utility Extensions
- **Transform Extensions**: Enhanced transform manipulation utilities
- **ScriptableObject Extensions**: Improved ScriptableObject workflow tools
- **MonoBehaviour Extensions**: Lifecycle and node extension utilities
- **String & List Extensions**: Common programming utilities

#### 📊 Package Analysis
- **Dependency Visualization**: Clear visualization of package dependencies
- **Assembly Inspection**: Detailed assembly dependency analysis
- **Manifest Management**: Automatic Unity manifest.json management

### Installation
**Recommended: Install via Unity Package Manager (Git URL)**

1. Open Unity Package Manager (`Window > Package Manager`)
2. Click the `+` button in the top-left corner
3. Select `Add package from git URL...`
4. Enter the Git URL: `https://github.com/RED-CANDLE-GAMES-CO-LTD/MonoFSM_Core_Utility.git`
5. Click `Add` and Unity will automatically download and install the package

**For Contributors: Using Git Submodule**
If you want to contribute to this package or need to modify the source code:

1. Navigate to your Unity project's `Packages` folder
2. Add as a submodule:
   ```bash
   git submodule add https://github.com/RED-CANDLE-GAMES-CO-LTD/MonoFSM_Core_Utility.git MonoFSM_Core_Utility
   ```
3. Initialize and update the submodule:
   ```bash
   git submodule update --init --recursive
   ```

**Alternative: Manual Installation**
1. Clone or download this repository
2. Place the `MonoFSM_Core_Utility` folder in your Unity project's `Packages` folder
3. Unity will automatically recognize and import the package

### Usage

#### Access the Git Dependency Installer
Navigate to: `Tools > MonoFSM > Dependencies > Git Dependencies Installer`

The installer provides two main tabs:
- **Git Dependencies Installation**: Manage and install Git-based packages
- **Assembly Analysis & Package.json Update**: Analyze dependencies and update configurations

#### Key Workflows
1. **Install Git Dependencies**: Select packages from the curated list and install them automatically
2. **Analyze Dependencies**: Review current project dependencies and identify missing packages
3. **Update Package Configuration**: Automatically update package.json files based on assembly analysis

### Requirements
- Unity 2022.3 LTS or later
- Git installed on your system
- Newtonsoft.Json package (automatically handled)

### Package Information
- **Name**: com.monofsm.utility
- **Version**: 0.1.0
- **Author**: Red Candle Games
- **Unity Version**: 2022.3+

---

## 中文版本

### 概述
MonoFSM Core Utility 是一個全面的 Unity 編輯器工具，提供進階的套件管理和 Git 依賴安裝功能。此工具透過直觀的編輯器介面簡化了 Unity 套件及其依賴項的管理過程。

### 主要功能

#### 🔧 Git 依賴管理器
- **視覺化套件��理**: 易於使用的編輯器視窗，用於管理基於 Git 的 Unity 套件
- **自動依賴解析**: 智能分析和安裝所需的依賴項
- **組件分析**: 進階的組件依賴分析，並自動更新 package.json
- **批次操作**: 一次性安裝多個套件和依賴項

#### 🛠 實用工具擴展
- **Transform 擴展**: 增強的 Transform 操作工具
- **ScriptableObject 擴展**: 改進的 ScriptableObject 工作流程工具
- **MonoBehaviour 擴展**: 生命週期和節點擴展工具
- **字串與清單擴展**: 常用的程式設計工具

#### 📊 套件分析
- **依賴視覺化**: 清晰的套件依賴關係視覺化
- **組件檢查**: 詳細的組件依賴分析
- **清單管理**: 自動 Unity manifest.json 管理

### 安裝方式
**推薦：透過 Unity 套件管理器安裝（Git URL）**

1. 開啟 Unity 套件管理器（`視窗 > 套件管理器`）
2. 點擊左上角的 `+` 按鈕
3. 選擇 `從 git URL 新增套件...`
4. 輸入 Git URL：`https://github.com/RED-CANDLE-GAMES-CO-LTD/MonoFSM_Core_Utility.git`
5. 點擊 `新增`，Unity 將自動下載並安裝該套件

**對於貢獻者：使用 Git 子模組**
如果您想為此套件做出貢獻或需要修改源代碼：

1. 進入您的 Unity 專案的 `Packages` 資料夾
2. 添加為子模組：
   ```bash
   git submodule add https://github.com/RED-CANDLE-GAMES-CO-LTD/MonoFSM_Core_Utility.git MonoFSM_Core_Utility
   ```
3. 初始化並更新子模組：
   ```bash
   git submodule update --init --recursive
   ```

**替代方案：手動安裝**
1. 複製或下載此存儲庫
2. 將 `MonoFSM_Core_Utility` 資料夾放置在您的 Unity 專案的 `Packages` 資料夾中
3. Unity 會自動識別並匯入套件

### 使用方法

#### 存取 Git 依賴安裝器
導航至：`Tools > MonoFSM > Dependencies > Git Dependencies Installer`

安裝器提供兩個主要標籤：
- **Git Dependencies 安裝**: 管理和安裝基於 Git 的套件
- **Assembly 分析與 Package.json 更新**: 分析依賴項並更新配置

#### 主要工作流程
1. **安裝 Git 依賴項**: 從精選清單中選擇套件並自動安裝
2. **分析依賴項**: 檢查當前專案依賴項並識別缺少的套件
3. **更新套件配置**: 根據組件分析自動更新 package.json 檔案

### 系統需求
- Unity 2022.3 LTS 或更高版本
- 系統已安裝 Git
- Newtonsoft.Json 套件（自動處理）

### 套件資訊
- **名稱**: com.monofsm.utility
- **版本**: 0.1.0
- **作者**: Red Candle Games
- **Unity 版本**: 2022.3+

---

### License
This project is licensed under the terms specified in the LICENSE file.

### Support
For issues and support, please contact: jerryee@redcandlegames.com
