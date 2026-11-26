# 收件箱默认未读筛选实施计划

> **For agentic workers:** 按步骤执行并逐项核对；遵守仓库 `AGENTS.md`，不得自动提交。

**Goal:** 收件箱在 URL 未指定阅读状态时默认只显示未读，明确筛选值及其他视图行为不变。

**Architecture:** 在共享的 `parseLibraryQuery` 中依据视图和 `status` 参数决定状态默认值。现有序列化和服务端筛选机制继续工作。

**Tech Stack:** TypeScript、Next.js、Vitest

## Global Constraints

- 仅修改实现此默认筛选所需的代码。
- 不创建 Git 提交。
- 未明确要求验证时不运行测试。

---

## 文件结构

- `src/web/src/lib/library-query.ts`：解析缺省阅读状态。
- `src/web/src/lib/library-query.test.ts`：现有查询解析单元测试文件；本次不新增测试，遵循未要求测试的约束。

### Task 1: 收件箱缺省状态为未读

**Files:**
- Modify: `src/web/src/lib/library-query.ts`

**Interfaces:**
- Consumes: `parseLibraryQuery(params: URLSearchParams): LibraryQuery`
- Produces: 查询参数缺省时，收件箱返回 `status: ['unread']`；URL 明确给出状态时返回解析后的显式值，其他视图缺省仍返回空数组。

- [ ] 计算归一化后的 `view` 后，解析 URL 中有效的状态值。
- [ ] 仅当归一化视图为 `inbox` 且 URL 未包含 `status` 参数时，返回 `['unread']`。
- [ ] 检查 diff，确认显式 `status` 与非收件箱默认行为未改变。
