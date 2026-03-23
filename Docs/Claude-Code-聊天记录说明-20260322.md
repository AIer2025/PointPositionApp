# Claude Code 聊天记录说明

## 保存位置

| 类型 | 位置 | 说明 |
|------|------|------|
| **会话历史** | `C:\Users\littl\.claude\sessions\` | 本地会话数据 |
| **项目记忆** | `C:\Users\littl\.claude\projects\c--TestClaude-PointPositionApp\memory\` | 跨会话持久化的知识 |
| **文件变更历史** | `C:\Users\littl\.claude\file-history\` | 文件修改记录 |

## 保存时长

- CLI 会话本身**不会永久保存完整对话内容**，关闭后对话上下文就丢失了
- **记忆文件**（memory）会一直保留，下次对话自动加载
- 如果使用 VS Code 扩展，会话在扩展关闭前可用

## 如何备份

最简单的方式是备份整个 `.claude` 目录：

```bash
# 复制到备份位置
cp -r ~/.claude ~/claude-backup-20260322
```

## 如何查看

- 在对话中输入 `/memory` 可以查看和管理记忆文件
- 直接用文件管理器打开 `C:\Users\littl\.claude\` 浏览所有数据
- 记忆文件都是 Markdown 格式，可以直接用编辑器打开

## 建议

如果有重要的分析结论或技术决策，保存为项目文件（如 `汇川PLC联调注意事项.md`）是最可靠的方式，它们跟代码一起被 Git 管理，不会丢失。
