# 点位配置软件使用指南

## 界面概览

### 1. 顶部工具栏（操作控制区）

| 功能 | 说明 |
|------|------|
| 🔌 连接PLC | 建立 Modbus TCP 连接到 PLC（需在设置中配置 IP/端口） |
| ⛔ 断开PLC | 断开 PLC 连接 |
| **■ 急停** | **红色大按钮** - 立即停止所有轴运动，取消进行中的操作 |
| 复位急停 | 清除急停状态（仅在触发急停后显示）|
| 🏠 全轴回原点 | 按优先级（Z/Z1轴优先）依次回原点到 0mm 位置 |
| 🔄 刷新数据 | 重新加载配置和数据库数据 |
| ⚙ 设置 | 打开设置窗口，配置 PLC 参数、轴参数、夹爪参数等 |

### 2. 左侧导航栏（📂 项目导航）

- **项目树形结构**：展示所有项目、模块及其包含的工作站
- **操作**：
  - 点击节点查看该级数据
  - 展开/收缩查看下级数据
  - 选中工作站后，右侧显示对应的点位网格

### 3. 中间区域（点位管理核心）

#### 3.1 信息栏
显示当前选中的项目/模块/工作站路径和选中单元格的坐标信息

#### 3.2 点位网格（绿/灰/黄色网格）
- **绿色网格**：已保存点位
- **灰色网格**：空网格（未保存点位）
- **黄色网格**：当前选中单元格

**网格格式**：`行号-列号` 例如 `1-1`, `2-3`

#### 3.3 和右侧的按钮

```
| 💾 保存点位 | 🗑 清除点位 | 🎯 跳转点位 | 📋 批量复制(行1→全部) |
```

### 4. 右侧控制面板（🔧 轴控制）

每个轴包含以下控制元素：

#### 4.1 轴状态行
```
[轴名] √使能    当前: [---- mm]
```
- **轴名**：X, Y, R, Z1, Z2
- **使能复选框**：勾选可使能轴
- **当前位置**：实时读取的轴位置

#### 4.2 点动控制行
```
◀  ▶   手动: [--] 自动: [-]
```
- **◀ / ▶**：按住鼠标按键进行点动（手动正向/反向）
- **手动速度**：点动时的速度（mm/s）
- **自动速度**：绝对运动时的速度（mm/s）

#### 4.3 目标运动行
```
目标: [----] mm   [▷绝对运动]  [⌂回零]
```
- **目标位置**：输入目标 mm 坐标
- **▷绝对运动**：移动轴到目标位置
- **⌂回零**：快速回到 0mm 原点

### 5. 夹爪控制（🤏 夹爪控制）

```
[🔓打开]  [🔒关闭]  [🔄旋转]

打开位: [--]    关闭位: [--]
角度:    [--]    关闭力矩: [--]
打开力矩: [--]
```

### 6. 底部状态栏

显示 PLC 连接状态、数据库连接状态、操作提示信息和当前时间

---

## 常见操作流程

### 流程 A：记录一个新点位

**步骤：**

1. **连接 PLC**
   - 点击 `🔌 连接PLC` 按钮
   - 状态栏显示 🟢 PLC 已连接

2. **使能轴**
   - 在右侧控制面板中，对需要使用的轴勾选 `√使能`
   - 提示：若未检查是否使能，系统会自动检查（需启用 RequireEnableBeforeMotion 配置）

3. **移动轴到目标位置**
   - **点动方式**：
     - 按住 ◀ 或 ▶ 按钮点动，松开后停止
   - **精确运动**：
     - 设置 **手动速度**（mm/s），例如 50
     - 按住 ◀/▶ 进行微调

   - **绝对运动**：
     - 在 **目标** 框输入位置，例如 `100`
     - 点击 **▷绝对运动** 按钮
     - 系统会使用 **自动速度** 移动到目标位置

4. **调试到理想位置**
   - 左侧导航栏选中工作站，中间显示点位网格
   - 右侧显示 5 个轴的实时位置
   - 微调直到位置满意

5. **保存点位**
   - **方式一**：点击 `💾 保存点位` 按钮
   - **方式二**：在网格上右键点击单元格 → `💾 保存当前位置`
   - **提示**：若 PLC 未连接，会保存 X=0 Y=0 Z=0 Z1=0 R=0 的默认点位
   - 状态栏显示 "点位已保存: [行,列]"
   - 网格颜色从灰色变为 🟩 绿色

---

### 流程 B：跳转到已保存的点位（防撞运动）

**前置条件**：点位必须已保存（绿色网格）且 PLC 已连接

**步骤：**

1. **选择目标点位**
   - 在中间网格上点击已保存的点位（绿色）

2. **确认运动**
   - 点击 `🎯 跳转点位` 按钮
   - 右键菜单 → `🎯 跳转到此点`
   - 弹出确认对话框，显示目标坐标
   - 检查坐标无误后点击 **OK**

3. **系统自动执行防撞运动顺序**
   - **① 抬起 Z 轴到安全高度**（配置项 SafeZHeight，默认 0mm）
     - 等待 Z 轴到位（基于位置反馈，容差 0.5mm，超时 30s）
   - **② 移动 XY/R 轴到目标位置**
     - 使用自动速度并行运动所有水平轴
     - 等待所有轴到位
   - **③ 下降 Z 轴到目标位置**
     - 所有水平轴到位后，最后下降 Z 轴
   - **④ 完成**
     - 状态栏显示 "已跳转到点位: [行,列]"

4. **参数说明**
   - `PositionTolerance`：到位容差，默认 0.5mm
   - `MotionTimeoutMs`：单轴运动超时时间，默认 30000ms
   - 若任何轴超时，运动停止，Z 轴保持安全高度

---

### 流程 C：清除点位

1. 在网格上选中已保存的点位（绿色）
2. 点击 `🗑 清除点位` 或右键 → `🗑 清除点位`
3. 点位从数据库中删除，网格变回灰色

---

### 流程 D：批量复制第 1 行到所有行

**场景**：工作站的第 1 行点位记录完毕，其他行需要相同的 X/Y 坐标，仅 Y 坐标递增

1. 先保存第 1 行的所有点位（1-1, 1-2, ..., 1-6）
2. 点击 `📋 批量复制(行1→全部)` 按钮
3. 系统自动：
   - 复制第 1 行的每个点位
   - 按行间距 `SpaceRow`（配置项）向下递增 Y 坐标
   - 保存到第 2、3、4 行
   - 存储到数据库

---

## 坐标保存到数据库的过程

### 1. 数据库结构

```sql
CREATE TABLE PointPosition (
    PointId INTEGER PRIMARY KEY,
    OwnerType VARCHAR(50),        -- 所属类型: "Project"/"Module"/"Station"
    OwnerId INTEGER,              -- 所属对象 ID
    RowIndex INTEGER,             -- 行号 (从 0 开始)
    ColIndex INTEGER,             -- 列号 (从 0 开始)
    X REAL,                       -- X 轴坐标 (mm)
    Y REAL,                       -- Y 轴坐标 (mm)
    Z REAL,                       -- Z 轴坐标 (DB中的Z字段，实际对应Axes[3])
    Z1 REAL,                      -- Z1 轴坐标 (DB中的Z1字段，实际对应Axes[4])
    R REAL,                       -- R 轴旋转角度 (°)
    ClawInfoId INTEGER            -- 关联的夹爪配置 ID
);
```

### 2. 坐标读取与映射

当按下 `💾 保存点位` 时，代码执行以下步骤：

#### 步骤 1：确定所有者信息
```
OwnerType = 从左侧导航树获取 (Project/Module/Station)
OwnerId = 对应对象的 ID
RowIndex = 当前选中网格的行索引
ColIndex = 当前选中网格的列索引
```

#### 步骤 2：读取 PLC 的轴位置值

**轴映射关系**：
```
Axes[0] → X 轴    → 数据库 X 字段
Axes[1] → Y 轴    → 数据库 Y 字段
Axes[2] → R 轴    → 数据库 R 字段
Axes[3] → Z1 轴   → 数据库 Z 字段
Axes[4] → Z2 轴   → 数据库 Z1 字段
```

**代码**（MainViewModel.cs：782-813）：
```csharp
// 若 PLC 已连接，读取实时位置；若未连接，默认为 0
var point = new PointPosition
{
    OwnerType = _currentOwnerType,
    OwnerId = _currentOwnerId,
    RowIndex = _selectedCell.RowIndex,
    ColIndex = _selectedCell.ColIndex,
    X = Axes.Count > 0 ? Axes[0].CurrentPosition : 0,
    Y = Axes.Count > 1 ? Axes[1].CurrentPosition : 0,
    R = Axes.Count > 2 ? Axes[2].CurrentPosition : 0,
    Z = Axes.Count > 3 ? Axes[3].CurrentPosition : 0,   // Z1轴 → DB.Z
    Z1 = Axes.Count > 4 ? Axes[4].CurrentPosition : 0,  // Z2轴 → DB.Z1
};

// 校验：检查位置值是否有效（NaN 表示读取失败）
if (float.IsNaN((float)point.X) || ...) {
    StatusMessage = "保存失败: 部分轴位置读取无效(NaN)";
    return;
}

await _db.SavePointAsync(point);
```

#### 步骤 3：检查数据库中是否已存在

**数据库服务** (DatabaseService.cs：299-325)：
```csharp
public async Task SavePointAsync(PointPosition point)
{
    // 查询是否存在相同的 OwnerType / OwnerId / RowIndex / ColIndex 的点位
    var existing = await _connection.QueryFirstOrDefaultAsync<PointPosition>(
        "SELECT * FROM PointPosition WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId
         AND RowIndex=@RowIndex AND ColIndex=@ColIndex",
        new { point.OwnerType, point.OwnerId, point.RowIndex, point.ColIndex });
}
```

#### 步骤 4：执行 INSERT 或 UPDATE

- **若不存在**（首次保存）：执行 **INSERT**
  ```sql
  INSERT INTO PointPosition (OwnerType, OwnerId, RowIndex, ColIndex, X, Y, Z, Z1, R, ClawInfoId)
  VALUES (@OwnerType, @OwnerId, @RowIndex, @ColIndex, @X, @Y, @Z, @Z1, @R, @ClawInfoId)
  ```

- **若存在**（再次更新）：执行 **UPDATE**
  ```sql
  UPDATE PointPosition SET X=@X, Y=@Y, Z=@Z, Z1=@Z1, R=@R, ClawInfoId=@ClawInfoId
  WHERE PointId=@PointId
  ```

#### 步骤 5：更新 UI

```csharp
_selectedCell.HasPoint = true;          // 标记为已保存
_selectedCell.Point = point;            // 缓存点对象
StatusMessage = $"点位已保存: [{RowIndex},{ColIndex}]";
// UI 网格颜色自动变为绿色
```

### 3. 数据库文件位置

- **文件名**：`pointposition.db`
- **存储位置**：应用程序根目录（或通过配置指定）
- **格式**：SQLite 3 数据库
- **访问工具**：可用 SQLite 管理工具（如 DB Browser for SQLite）打开查看

### 4. 坐标读取流程

当程序启动或刷新时，会从数据库加载所有点位：

```csharp
var points = await _db.GetPointsAsync(ownerType, ownerId);
// 返回 List<PointPosition>，按 RowIndex, ColIndex 排序

foreach (var point in points)
{
    // 将点位绑定到对应的网格单元格
    gridCell.Point = point;
    gridCell.HasPoint = true;  // 网格变绿
}
```

---

## 安全保护功能说明

### 1. 急停（紧急停止）

**触发方式**：
- 点击红色 **■ 急停** 按钮
- 键盘快捷键 Esc（如配置）

**执行动作**：
1. 立即停止所有轴的点动指令
2. 取消进行中的运动序列（如 GotoPoint）
3. 向 PLC 写入全 0 线圈，停止所有运动
4. UI 变为禁用状态，状态栏显示 "!!! 急停已触发 !!!"

**恢复操作**：
- 点击 **复位急停** 按钮清除急停状态

### 2. 软限位（位置范围保护）

**配置**（appsettings.json）：
- 每个轴可设置 `SoftLimitMin` 和 `SoftLimitMax`
- 例如：X 轴 [-10, 600] mm

**保护机制**：
- 用户在 `目标` 框输入超出范围的坐标时，系统提示并拦截运动指令
- 模拟模式中，若坐标越界，轴会被严格钳位到限位位置

### 3. 速度钳位

**手动速度限制**：
- `MaxManualSpeed`（默认 200 mm/s）
- 点动时输入的速度若超过此值，会自动降低

**自动速度限制**：
- `MaxAutoSpeed`（默认 500 mm/s）
- 绝对运动时的速度若超过此值，会自动降低

### 4. 运动前安全检查

执行任何运动前，系统会检查：
- ✓ PLC 已连接
- ✓ 未处于急停状态
- ✓ 目标轴已使能（若 RequireEnableBeforeMotion = true）

若检查失败，状态栏会显示原因，运动被拦截。

### 5. 防撞运动顺序（GotoPoint）

跳转到点位时自动执行三段式运动：
1. **抬起 Z 轴到安全高度** → 等待到位
2. **平移 XY/R 轴** → 等待到位
3. **下降 Z 轴到目标高度** → 等待到位

若任何环节超时或被急停，运动停止，Z 轴保持安全高度，防止碰撞。

---

## 快速参考

### 键盘快捷键
- **Esc** ：触发急停（如配置）

### 常用速度值（mm/s）
- 粗定位：100 - 200
- 精准定位：20 - 50
- 高速移动：300 - 500

### 常见提示信息
| 提示 | 含义 | 解决方案 |
|-----|------|--------|
| "PLC未连接" | PLC 掉线 | 检查网络，点击 🔌 连接PLC |
| "轴 X 未使能" | 轴未启动 | 勾选轴的 √使能 复选框 |
| "目标位置超出软限位" | 坐标越界 | 修改目标值，使其在允许范围内 |
| "跳转失败：Z轴抬升超时" | Z 轴无响应 | 检查 Z 轴硬件，查看日志报错 |
| "!!! 急停已触发 !!!" | 正处于急停状态 | 点击复位急停按钮恢复 |

---

## 数据库导出 / 备份

点位数据存储在 `pointposition.db` 中，可直接：
- **复制文件**备份
- **用 SQLite 工具打开**查看/编辑
- **导出为 CSV**用 Excel 查看
- **定期备份**避免数据丢失

---

## 常见高级操作

### 导入历史数据
1. 获取旧数据库文件 `pointposition.db`
2. 放在应用程序根目录
3. 重启应用，数据自动加载

### 修改已保存的点位
1. 移动轴到新位置
2. 在网格上选中该点位（绿色）
3. 点击 `💾 保存点位`
4. 系统自动 UPDATE 该点位的坐标

### 调试模拟模式（无硬件）
- appsettings.json 中设置 `"SimulationMode": true`
- 可模拟 PLC 运动，不需真实硬件
- 用于界面开发、数据库测试

---

**最后更新**：2026-04-02
**版本**：1.1（包含安全保护功能）

