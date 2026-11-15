# 标签管理（新建、删除、从已有标签中选）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在标签列表页通过对话框新建标签、在卡片上直接删除标签；阅读页和文库卡片加标签时可以从已有标签中选择，没有匹配的名称时直接新建。

**Architecture:**
- 后端新增 `POST /api/tags`：先用 FluentValidation 校验请求，再由 `TagService` 调用 `ITagWriter` 保存。标签基础设施层处理唯一索引冲突，接口返回 409。
- 标签页新增 `TagDialog` 组件，`tags/page.tsx` 负责打开对话框和卡片删除。删除沿用现有的 `DELETE /api/tags/{id}`，完成后调用 `notifyLibraryMutation()`。
- `TagManager` 的名称输入框改为 ARIA combobox，匹配规则写成纯函数 `tagSuggestions`。添加标签仍走现有的 `POST /api/articles/{id}/tags`，后端不需要改。

**Tech Stack:** ASP.NET Core 9, EF Core + Npgsql, FluentValidation, xUnit + FluentAssertions + Testcontainers；Next.js 15, React, Arc UI（Radix Dialog）, CSS Modules, Vitest + happy-dom。

**Spec:** `docs/designs/2026-11-14-tag-management-design.md`

## Global Constraints

- **名称**：去掉首尾空白后长度为 1–100（C# `string.Length`）。前端的两个名称输入框都设 `maxLength={100}`；`TagManager` 原来的 `maxLength={20}` 改为 100。
- **颜色**：省略或空白时，后端默认 `#3B82F6`；否则必须匹配 `^#[0-9A-Fa-f]{6}$`。
- **重名（API 和标签页对话框）**：去掉首尾空白后，**区分大小写的完全相同**名称 → 409 `{ "existingTagId": "<guid>" }`。
- **同名（`TagManager` 下拉）**：去掉首尾空白后，按 `toLocaleLowerCase()` 比较，**不区分大小写**。
- **400 响应**：`{ "error": "<信息>" }`，写法与 `FeedsController` 一致。
- **201 响应**：响应体为 `TagWithArticleCountDto`（`articleCount = 0`），`Location` 指向 `/api/tags/{id}`。
- **前端颜色列表**（顺序固定）：`#3b82f6 #ef4444 #10b981 #f59e0b #8b5cf6 #f97316 #06b6d4 #84cc16 #ec4899 #6b7280`，默认 `#3b82f6`。
- **标签页固定文案**：
  - 按钮和对话框标题：「新建标签」；
  - 对话框按钮：「创建」/「正在创建…」、「取消」；
  - 对话框错误：「已有同名标签」「标签名称无效」「创建失败，请重试」；
  - 创建成功提示：「已创建「X」」；
  - 删除确认的标题和确认按钮：「删除标签」；
  - 删除确认文字：`删除「X」标签？它会从 N 篇文章中移除，文章本身不受影响。`（N>0），`删除「X」标签？`（N=0）；
  - 删除失败：「标签删除失败，请重试。」；
  - 删除按钮的 `aria-label`：「删除标签「X」」。
- **`TagManager` 固定文案**：
  - 占位文字「搜索或新建标签…」；
  - 列表状态：「正在加载标签…」「标签加载失败」「没有匹配的标签」；
  - 新建选项「创建「X」」，颜色区域标题「新标签颜色」，不可选提示「已添加」；
  - 按钮「添加」/「添加中…」、「完成」（替换原来的「取消」）；
  - 添加失败：「添加标签失败，请重试」。
- **文件编码**：`TagService.cs`、`ITagService.cs` 是 **UTF-16 LE（带 BOM）+ CRLF**。修改后 `file <path>` 的输出必须与修改前相同。其他被修改的 C# 文件同样保持原编码和换行。
- **保留现有行为**：
  - 加标签继续沿用 `TagService.CreateTagAsync` 和 `AddTagToArticleCommandHandler`。默认颜色由 `TagColor.Create` 处理；
  - 标签详情页、`BatchToolbar`、Arc 组件；
  - `TagManager` 中已加标签的展示和移除。

## Review Focus

1. **首尾空白导致的重名**：已有 `"阅读"` 时提交 `"  阅读  "`，应该返回 409，不能返回 500 或建出第二个标签。由 Task 1 的 `CreateTag_DuplicateName…` 覆盖；`TagManager` 中由 Task 4 的 `" go "` 用例覆盖。
2. **长度正好在边界上**：去掉首尾空白后正好 100 个字符应该创建成功；101 个字符返回 400。由 Task 1 的 `CreateTag_AcceptsHundredCharactersAfterTrim` 和 `CreateTag_InvalidInput…` 覆盖。
3. **连点或连按回车**：对话框和 `TagManager` 都只能发出一个请求。由 Task 2 的 `submits once while a create request is pending` 和 Task 4 的 `adds once while a request is pending` 覆盖。
4. **点删除按钮跳转到了详情页**：删除按钮不能放在 `<a>` 里面。由 Task 3 的 `delete button is outside the card link` 覆盖。
5. **表单展开时移除了一个已加标签**：这个标签应该立刻回到下拉列表中，所以列表必须根据 `tags` prop 计算，不能缓存打开时的结果。由 Task 4 的 `a tag removed while the form is open returns to the list` 覆盖。

---

## File Structure

| 文件 | 职责 |
|---|---|
| Modify `src/backend/MiniCc.Api/Core/TagNs/Application/Commands/CreateTagCommand.cs`，Create `CreateTagCommandValidator.cs` | 复用创建命令作为请求，由 Application 校验名称和颜色。 |
| Create `src/backend/MiniCc.Api/Core/TagNs/Application/DTOs/CreateTagOutcome.cs` | 创建成功时包含 DTO，冲突时提供已有标签的 ID。 |
| Modify `src/backend/MiniCc.Api/Core/TagNs/Application/Services/ITagService.cs`、`TagService.cs` | 新增 `TryCreateTagAsync`。 |
| Create `src/backend/MiniCc.Api/Core/TagNs/Application/Services/ITagWriter.cs`、`Core/TagNs/Infrastructure/TagWriter.cs` | 查重、委托 Unit of Work 保存，并处理标签名称唯一索引冲突。 |
| Modify `src/backend/MiniCc.Api/Core/TagNs/Infrastructure/EntityConfigurations/TagConfiguration.cs`、`Core/DependencyInjection.cs` | 复用现有索引名并注册 `ITagWriter`。 |
| Modify `src/backend/MiniCc.Api/Core/TagNs/Domain/AggregatesModel/ValueObjects/TagColor.cs` | 共享六位 ASCII 十六进制颜色规则和默认值。 |
| Modify `src/backend/MiniCc.Api/Controllers/TagNs/TagsController.cs` | 新增 `[HttpPost] CreateTag`。 |
| Create `test/backend/MiniCc.Api.Test/Infrastructure/TagApiTests.cs` | `partial class LibraryApiTests`，复用 `CreateFactory` / `AuthenticatedClient` / `fixture`。 |
| Create `src/frontend/src/lib/tag-colors.ts` | 共享颜色常量。 |
| Create `src/frontend/src/app/components/TagColorOptions.tsx` 和对应 CSS Module | 对话框和阅读页使用同一组预设颜色按钮。 |
| Modify `src/frontend/src/lib/api.ts` | `tagApi.createTag`。 |
| Create `src/frontend/src/app/pages/tags/TagDialog.tsx` + `TagDialog.test.tsx` | 新建对话框，参照 `feeds/FeedDialog.tsx`。 |
| Modify `src/frontend/src/app/pages/tags/page.tsx`、`tags.module.css` | 新建按钮、空状态、卡片删除。 |
| Create `src/frontend/src/app/pages/tags/page.test.tsx` | 标签页测试。 |
| Create `src/frontend/src/lib/tag-suggestions.ts` + `tag-suggestions.test.ts` | 下拉选择的匹配规则（纯函数）。 |
| Modify `src/frontend/src/app/pages/article/[id]/components/TagManager.tsx`、`TagManager.module.css`、`TagManager.test.tsx` | combobox。 |
| Modify `CLAUDE.md` | API Structure 和 Frontend Features。 |

---

### Task 1: 后端 `POST /api/tags` 与删除行为测试

**Files:**
- Modify: `src/backend/MiniCc.Api/Core/TagNs/Application/Commands/CreateTagCommand.cs`（UTF-16）
- Create: `src/backend/MiniCc.Api/Core/TagNs/Application/Commands/CreateTagCommandValidator.cs`
- Create: `src/backend/MiniCc.Api/Core/TagNs/Application/DTOs/CreateTagOutcome.cs`
- Modify: `src/backend/MiniCc.Api/Core/TagNs/Application/Services/ITagService.cs`（UTF-16）
- Modify: `src/backend/MiniCc.Api/Core/TagNs/Application/Services/TagService.cs`（UTF-16）
- Create: `src/backend/MiniCc.Api/Core/TagNs/Application/Services/ITagWriter.cs`、`Core/TagNs/Infrastructure/TagWriter.cs`
- Modify: `src/backend/MiniCc.Api/Core/TagNs/Infrastructure/EntityConfigurations/TagConfiguration.cs`、`Core/DependencyInjection.cs`（后者为 UTF-16）
- Modify: `src/backend/MiniCc.Api/Core/TagNs/Domain/AggregatesModel/ValueObjects/TagColor.cs`
- Modify: `src/backend/MiniCc.Api/Controllers/TagNs/TagsController.cs`
- Modify: `CLAUDE.md`（API Structure）
- Test: `test/backend/MiniCc.Api.Test/Infrastructure/TagApiTests.cs`

**Interfaces:**
- Produces（HTTP）：`POST /api/tags`，body `{ name: string, color?: string }`。返回 201 `{ id, name, color, createdAt, articleCount: 0 }`、409 `{ existingTagId }` 或 400 `{ error }`。Task 2 依赖这个契约。
- Produces（C#）：
  - `CreateTagCommand(string? Name, string? Color)` 作为创建请求。
  - `CreateTagCommandValidator : AbstractValidator<CreateTagCommand>`
  - `CreateTagOutcome.Created(TagWithArticleCountDto Tag)` 或 `CreateTagOutcome.Conflict(Guid ExistingTagId)`。
  - `ITagService.TryCreateTagAsync(CreateTagCommand request, CancellationToken cancellationToken = default)` 返回 `Task<CreateTagOutcome>`。

- [ ] **Step 1: 写失败的测试**

`TagApiTests.cs` 的写法与 `FeedApiTests.cs` 相同：`namespace MiniCc.Api.Test.Infrastructure; public partial class LibraryApiTests { … }`。所有测试共享同一个数据库，名称都要加上 `g = Guid.NewGuid().ToString("N")` 以保证唯一。

```csharp
[Fact] CreateTag_ReturnsCreatedTrimmedTagAndListsIt
//   POST { name = $"  阅读 {g}  ", color = "#ef4444" }
//   → 201；Location 以 $"/api/tags/{id}" 结尾；body.name == $"阅读 {g}"；body.color == "#ef4444"；body.articleCount == 0
//   → GET /api/tags?search={g} 恰好返回这一条

[Fact] CreateTag_DefaultsColorWhenOmitted
//   POST { name = $"Default {g}" } → 201；body.color == "#3B82F6"

[Fact] CreateTag_AcceptsHundredCharactersAfterTrim
//   name = "  " + new string('字', 68) + g + "  "（去掉空白后正好 100 个字符）→ 201；body.name.Length == 100

[Fact] CreateTag_DuplicateNameReturnsConflictWithExistingIdButCaseVariantSucceeds
//   POST $"Dup {g}" → 201（记为 id1）
//   POST $"  Dup {g}  " → 409；body.existingTagId == id1
//   POST $"dup {g}" → 201
//   数据库中名称为 $"Dup {g}" 的标签只有 1 条

[Theory] CreateTag_InvalidInputReturnsBadRequest(string? name, string? color)
//   ("", null), ("   ", null), (new string('x', 101), null), ("ok", "red"), ("ok", "#12345"), ("ok", "#GGGGGG")
//   → 400；body.error 是非空字符串
//   （InlineData 中不能拼 Guid：在测试体内给 "ok" 加上 Guid 后缀，或者改用 MemberData）

[Fact] CreateTag_RequiresAuthentication
//   factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
//   POST → IsSuccessStatusCode == false；数据库中没有这个名称

[Fact] CreateTag_ConcurrentSameNameCreatesExactlyOne
//   同一个 factory 创建两个 AuthenticatedClient，用 Task.WhenAll 同时 POST 相同名称
//   → 两个状态码排序后为 [201, 409]；409 的 existingTagId 等于 201 返回的 id；数据库中这个名称只有 1 条

[Fact] DeleteTag_RemovesActiveTrashedAndFeedAssociationsOnly
//   准备数据（fixture.CreateContext）：
//     标签 T、U；正常文章 A1 带 T 和 U；回收站文章 A2 带 T（MoveToTrash）；订阅源 F（Feed.Create(...)）的默认标签为 [T]
//   DELETE /api/tags/{T} → 204
//   新开一个 context，用 IgnoreQueryFilters() 查询：
//     T 不存在；A1 存在，标签只剩 [U]；A2 仍存在，DeletedAt 不为空，没有标签；F 存在，没有标签；U 仍存在
```

- [ ] **Step 2: 运行测试，确认失败**

Run：`dotnet test test/backend/MiniCc.Api.Test --filter "FullyQualifiedName~LibraryApiTests.CreateTag|FullyQualifiedName~LibraryApiTests.DeleteTag"`

Expected：
- `CreateTag_*` 失败（405 或 404，因为还没有 POST 接口）；
- `DeleteTag_*` **通过**，它锁定的是现有行为。如果它失败，先停下来报告，不要改删除逻辑。

运行需要 Docker 兼容的守护进程。rootless Podman 可能需要设置 `DOCKER_HOST`、`TESTCONTAINERS_HOST_OVERRIDE=localhost`、`TESTCONTAINERS_RYUK_DISABLED=true`。

- [ ] **Step 3: 实现请求类型与校验**

在 `CreateTagCommandValidator.cs`（UTF-8，命名空间 `MiniCc.Api.Core.TagNs.Application.Commands`）中实现：
- `Name`：`Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= 100).WithMessage("标签名称必须为 1–100 个字符")`；
- `Color`：`Must(TagColor.IsValidHexColor).When(r => !string.IsNullOrWhiteSpace(r.Color)).WithMessage("颜色必须是 #RRGGBB 格式")`。

- [ ] **Step 4: 实现 `CreateTagOutcome`、`TryCreateTagAsync` 与 `TagWriter`**

**编码处理**：先用 `iconv -f UTF-16LE -t UTF-8` 转成 UTF-8 副本，编辑后再用 `iconv -f UTF-8 -t UTF-16LE` 转回。确认文件开头仍有 BOM `FF FE`，换行仍是 CRLF。

`TagService.TryCreateTagAsync` 用 `Tag.Create` 生成名称已去掉首尾空白的标签，再调用 `ITagWriter`。基础设施层的 `TagWriter` 通过现有仓储查重和登记，并调用 `IUnitOfWork.SaveChangesAsync`。保存命中标签名称唯一索引时，`TagWriter` 分离本次插入失败的实体，查回已有标签的 ID。创建成功时由应用服务生成 DTO。

- [ ] **Step 5: 实现控制器动作**

`TagsController.CreateTag` 返回 `ActionResult<TagWithArticleCountDto>` 并声明 201、400、409 的 OpenAPI 响应类型。校验失败返回 `{ error }`，已有标签返回 `{ existingTagId }`。创建成功时返回 `articleCount = 0`，通过 `LinkGenerator` 按现有标签详情路由生成小写 `Location`。

- [ ] **Step 6: 运行测试，确认通过**

1. Run：与 Step 2 相同。Expected：上面列出的测试全部 PASS（Theory 按用例计数）。
2. Run：`dotnet test`。Expected：失败数为 0。
3. Run：`file src/backend/MiniCc.Api/Core/TagNs/Application/Services/ITagService.cs src/backend/MiniCc.Api/Core/TagNs/Application/Services/TagService.cs`。Expected：两个文件都显示 `Unicode text, UTF-16, little-endian text, with CRLF line terminators`。

- [ ] **Step 7: 更新 `CLAUDE.md` 的 API Structure**

在 `/api/articles/...` 那一组后面加入：

```
GET    /api/tags                  # Tags with article counts (?search=)
POST   /api/tags                  # Create tag { name, color? }: 201 | 409 { existingTagId } | 400 { error }
DELETE /api/tags/{id}             # Delete tag; DB cascades ArticleTags/FeedTags, articles untouched
```

- [ ] **Step 8: 提交**

```bash
git add src/backend/MiniCc.Api/Controllers/TagNs src/backend/MiniCc.Api/Core/TagNs src/backend/MiniCc.Api/Core/DependencyInjection.cs test/backend/MiniCc.Api.Test/Infrastructure/TagApiTests.cs CLAUDE.md
git commit -m "feat(api): add POST /api/tags with duplicate-name conflict"
```

---

### Task 2: 新建对话框 `TagDialog`、`tagApi.createTag` 与共享颜色

**Files:**
- Create: `src/frontend/src/lib/tag-colors.ts` 与 `src/frontend/src/app/components/TagColorOptions.tsx`、`TagColorOptions.module.css`
- Modify: `src/frontend/src/lib/api.ts:93-112`
- Create: `src/frontend/src/app/pages/tags/TagDialog.tsx`
- Test: `src/frontend/src/app/pages/tags/TagDialog.test.tsx`

**Interfaces:**
- Consumes：Task 1 的 HTTP 契约。
- Produces：
  - `lib/tag-colors.ts`：`export const TAG_COLORS: readonly string[]`、`export const DEFAULT_TAG_COLOR = "#3b82f6"`。Task 4 也会用到。
  - `tagApi.createTag(name: string, color: string): Promise<TagWithArticleCount>`，调用 `POST /tags`，body 为 `{ name, color }`。
  - `TagDialog.tsx`：`export function getCreateTagErrorMessage(error: unknown): string`。
  - `TagDialog.tsx`：`export default function TagDialog({ onClose, onCreated }: { onClose: () => void; onCreated: (tag: TagWithArticleCount) => void })`。调用方打开时挂载、关闭时卸载（与 `FeedDialog` 相同），所以每次打开都是默认值。

- [ ] **Step 1: 写失败的测试 `TagDialog.test.tsx`**

准备方式：
- mock：`vi.mock('@/lib/api', () => ({ tagApi: { createTag: vi.fn() } }))`；
- 测试结构与 `FeedDialog.test.tsx` 相同，对话框从 `document.body` 中按 `[role="dialog"]` 查找；
- 给受控输入框赋值时，用 `Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(input, …)`，再派发 `input` 事件，参照 `LibraryToolbar.test.tsx:27`。

```ts
it('focuses the name input and disables create until a non-blank name is entered')
//   document.activeElement 是名称输入框；「创建」disabled；输入 "   " 后仍 disabled；输入 "阅读" 后可用
//   input.maxLength === 100；10 个颜色按钮中 aria-pressed="true" 的是 "选择颜色 #3b82f6"

it('creates with the trimmed name and chosen color, then reports the created tag')
//   点击 "选择颜色 #ef4444"，输入 "  阅读  "，提交表单
//   → createTag 以 ("阅读", "#ef4444") 调用；onCreated 收到 mock 返回的 tag

it('keeps the dialog open with input preserved and shows 已有同名标签 on 409')
//   createTag 以 new AxiosError('conflict', 'ERR_BAD_REQUEST', undefined, undefined, { status: 409, data: { existingTagId: 'x' } } as never) reject
//   → [role="alert"] 的文本为 "已有同名标签"；输入框的值仍是 "阅读"；onCreated 和 onClose 都没有被调用

it('maps 400 to the server message or 标签名称无效, and other failures to 创建失败，请重试')
//   直接测试 getCreateTagErrorMessage：
//   400 + { error: "标签名称必须为 1–100 个字符" } → 返回该文本；400 且没有 error → "标签名称无效"；
//   500 → "创建失败，请重试"；new Error() → "创建失败，请重试"

it('submits once while a create request is pending')
//   createTag 返回一个永不 resolve 的 Promise；连续提交两次 → createTag 只被调用 1 次；
//   按钮文字为 "正在创建…" 且 disabled；派发 Escape keydown → defaultPrevented === true，onClose 没有被调用
```

- [ ] **Step 2: 运行测试，确认失败**

Run（在 `src/frontend` 下）：`pnpm test -- src/app/pages/tags/TagDialog.test.tsx`
Expected：FAIL，提示无法解析 `./TagDialog`。

- [ ] **Step 3: 新建 `lib/tag-colors.ts`，新增 `tagApi.createTag`，实现 `TagDialog`**

`TagDialog` 的结构：
- 外层是 `<Dialog open onOpenChange={…}>` 加 `DialogContent`，属性为 `title="新建标签"`、`closeLabel="关闭新建标签对话框"`、`closeDisabled={pending}`。
- 请求进行中时，`onEscapeKeyDown` 和 `onInteractOutside` 都调用 `preventDefault`。
- `onOpenAutoFocus` 中先 `preventDefault`，再让名称输入框获得焦点。
- 底部依次是「取消」（secondary）和 `type="submit" form="tag-dialog-form"` 的「创建」。
- 名称用受控状态保存。
- 颜色放在 `<fieldset>` 中，`legend` 为「标签颜色」；每个颜色按钮带 `aria-label={`选择颜色 ${c}`}` 和 `aria-pressed`。
- 通过 Arc `Input` 的 `error` 属性在输入框下方显示可访问的错误提示。

提交与错误处理：
- 提交时先检查 `pending`，正在提交就直接返回。
- `getCreateTagErrorMessage` 的判断顺序：`axios.isAxiosError` → status 409 → status 400 且 `data.error` 是字符串 → status 400 → 其他情况。

样式可以复用 `feeds.module.css` 中的类，也可以新建 `TagDialog.module.css`，由实现者决定。

- [ ] **Step 4: 运行测试，确认通过**

Run：`pnpm test -- src/app/pages/tags/TagDialog.test.tsx`
Expected：全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/frontend/src/lib/tag-colors.ts src/frontend/src/lib/api.ts src/frontend/src/app/components/TagColorOptions.tsx src/frontend/src/app/components/TagColorOptions.module.css src/frontend/src/app/pages/tags/TagDialog.tsx src/frontend/src/app/pages/tags/TagDialog.module.css src/frontend/src/app/pages/tags/TagDialog.test.tsx
git commit -m "feat(web): add create-tag dialog and shared tag colors"
```

---

### Task 3: 标签页接入新建与卡片删除

**Files:**
- Modify: `src/frontend/src/app/pages/tags/page.tsx`
- Modify: `src/frontend/src/app/pages/tags/tags.module.css`
- Test: `src/frontend/src/app/pages/tags/page.test.tsx`

**Interfaces:**
- Consumes：
  - Task 2 的 `TagDialog`；
  - `tagApi.getTags`、`tagApi.deleteTag`；
  - `useConfirm()`、`useToast().show`；
  - `notifyLibraryMutation()`（`lib/library-mutations.ts`）。
- Produces：无。这是最末端的任务，没有其他任务依赖它。

- [ ] **Step 1: 写失败的测试 `page.test.tsx`**

准备方式：
- mock `@/lib/api` → `{ tagApi: { getTags, createTag, deleteTag } }`；
- mock `@/lib/library-mutations` → `{ notifyLibraryMutation: vi.fn() }`；
- mock `@/contexts/ToastContext` → `useToast: () => ({ show })`，`show = vi.fn()` 在模块作用域中创建；
- 页面外面包一层真实的 `ConfirmProvider`；确认对话框中的按钮要在 `document.body` 中查找，参照 `highlights/page.test.tsx`。

```ts
it('shows 新建标签 next to search, including when there are no tags')
//   getTags 返回 [] → 页面上有「新建标签」按钮，空状态文字中包含「新建标签」

it('reloads with the current search term after creating')
//   在搜索框输入 "阅" 并等待 300ms 防抖（用 vi.useFakeTimers 或 vi.waitFor），点击「新建标签」，通过 TagDialog 创建
//   → 对话框关闭；最后一次 getTags 的参数为 "阅"；show 以 "已创建「阅读」" 调用

it('delete button is outside the card link and does not navigate')
//   button[aria-label="删除标签「123」"] 存在，且 .closest('a') === null

it('confirms with the article count, deletes, removes the card and updates the counts')
//   准备两个标签，其中一个为 { name:'456', articleCount:3 }
//   点击删除 → 确认文字为 "删除「456」标签？它会从 3 篇文章中移除，文章本身不受影响。"
//   点击「删除标签」→ deleteTag 以该标签的 id 调用；卡片消失；统计显示 "1 个标签"；
//   notifyLibraryMutation 被调用 1 次；getTags 没有被再次调用

it('omits the count for unused tags and does nothing when cancelled')
//   articleCount 为 0 → 确认文字为 "删除「123」标签？"；点击「取消」→ deleteTag 没有被调用

it('keeps the card and shows a toast when delete fails')
//   deleteTag reject → 卡片还在；show 以 "标签删除失败，请重试。" 调用；notifyLibraryMutation 没有被调用
```

- [ ] **Step 2: 运行测试，确认失败**

Run：`pnpm test -- src/app/pages/tags/page.test.tsx`
Expected：FAIL，提示找不到「新建标签」按钮和删除按钮。

- [ ] **Step 3: 实现页面改动**

新增状态：
- `dialogOpen: boolean`；
- `deletingId: string | null`：正在删除的卡片，它的删除按钮不可用。

**新建标签**
- 按钮放在 `.controls` 中搜索框的右侧，使用 Arc `Button` 加 `Plus` 图标。正常列表和空状态下显示，加载中和加载失败时不显示。
- 创建成功后：`setDialogOpen(false)`；调用 `toast.show(`已创建「${tag.name}」`)`；调用 `void searchTags(searchTerm)` 刷新列表，这样不会整页闪回加载状态。
- 没有搜索词的空状态，说明文字改为「点击「新建标签」先建好标签，也可以在阅读文章时添加。」。有搜索词时的空状态文字不变。

**卡片结构**
- 外层 `<div className={styles.tagCard}>`。
- 里面依次放 `<Link className={styles.tagLink}>`（色点和信息），以及删除用的 `Button`（`variant="ghost" size="sm" iconOnly`，图标 `Trash2 size={15}`）。

**删除流程**
1. 根据 `articleCount` 生成确认文字。
2. 调用 `confirm(text, { title: "删除标签", confirmLabel: "删除标签" })`。
3. 确认后调用 `deleteTag`：
   - 成功：`setTags(prev => prev.filter(…))`，然后调用 `notifyLibraryMutation()`；
   - 失败：调用 `toast.show("标签删除失败，请重试。")`。
4. 最后 `setDeletingId(null)`。

- [ ] **Step 4: 修改样式 `tags.module.css`**

- `.tagCard`：保留边框、背景和悬停样式，去掉 `text-decoration`。
- `.tagLink`：`display:flex; flex:1; min-width:0; align-items:center; gap:12px; color:inherit; text-decoration:none`。
- 原来卡片上的 `:focus-visible` 轮廓改到 `.tagLink:focus-visible`。
- `.deleteButton`：默认 `opacity: 0`；`.tagCard:hover .deleteButton` 和 `.tagCard:focus-within .deleteButton` 时为 `opacity: 1`。
- `@media (hover: none), (pointer: coarse), (max-width: 600px)` 中显示删除按钮。`@media (hover: none)` 还会将删除按钮的 Arc 小号尺寸设为大号，使无悬停设备的点击区域至少为 44×44 CSS 像素。
- `.controls`：`display:flex; gap:12px; align-items:center; flex-wrap:wrap`。

- [ ] **Step 5: 运行测试，确认通过**

Run：`pnpm test -- src/app/pages/tags`
Expected：`page.test.tsx` 和 `TagDialog.test.tsx` 全部 PASS。

- [ ] **Step 6: 提交**

```bash
git add src/frontend/src/app/pages/tags
git commit -m "feat(web): create and delete tags from the tag list"
```

---

### Task 4: `TagManager` 从已有标签中选择或直接新建

**Files:**
- Create: `src/frontend/src/lib/tag-suggestions.ts`
- Test: `src/frontend/src/lib/tag-suggestions.test.ts`
- Modify: `src/frontend/src/app/pages/article/[id]/components/TagManager.tsx`
- Modify: `src/frontend/src/app/pages/article/[id]/components/TagManager.module.css`
- Modify: `CLAUDE.md`（Frontend Features）
- Test: `src/frontend/src/app/pages/article/[id]/components/TagManager.test.tsx`

**Interfaces:**
- Consumes：
  - Task 2 的 `TAG_COLORS`、`DEFAULT_TAG_COLOR`；
  - `tagApi.getTags(): Promise<TagWithArticleCount[]>`；
  - `articleApi.addTag(articleId, { name, color }): Promise<Tag>`。
- Produces：
  - `export function tagSuggestions(all: Tag[], attached: Tag[], input: string): { options: Tag[]; exact: Tag | null; attachedMatch: boolean; canCreate: boolean }`（`lib/tag-suggestions.ts`）。
  - `TagManager` 的 props 不变：`{ articleId, tags, onTagsChange, compact? }`。`Client.tsx` 和 `ArticleCard.tsx` 不需要改。

`tagSuggestions` 的规则：
- 设 `q = input.trim()`，`k = q.toLocaleLowerCase()`。
- `available`：`all` 中 id 不在 `attached` 里的标签。
- `options`：`q` 为空时返回全部 `available`；否则返回名称小写后包含 `k` 的 `available`。
- `exact`：`available` 中名称小写后等于 `k` 的那个标签；`q` 为空时为 `null`。
- `attachedMatch`：`q` 非空，并且 `attached` 中有名称小写后等于 `k` 的标签。
- `canCreate`：`q` 非空、`!attachedMatch`，并且 `all` 中没有名称小写后等于 `k` 的标签。

- [ ] **Step 1: 写失败的测试**

`tag-suggestions.test.ts`：

```ts
it('returns every tag not on the article when the input is blank')        // all=[Go,Gopher,.NET], attached=[.NET], "  " → options [Go,Gopher]; exact null; canCreate false
it('filters by case-insensitive substring after trimming')                 // " GO " → options [Go,Gopher]
it('finds the case-insensitive exact match and suppresses create')         // "go" → exact Go; canCreate false
it('flags names already on the article')                                   // ".net" → attachedMatch true; exact null; canCreate false; options []
it('allows creating a new name')                                           // "Rust" → options []; canCreate true
it('allows creating when all tags failed to load but not an attached name') // all=[], attached=[.NET]: "Rust" → canCreate true；".NET" → canCreate false
```

`TagManager.test.tsx`：mock 改为 `{ articleApi: { removeTag, addTag }, tagApi: { getTags } }`，并 mock `@/contexts/ToastContext` 让 `useToast` 返回 `{ show }`。原有的移除标签用例保留。数据：`getTags` 返回 `[Go #3b82f6, Gopher #10b981, .NET #8b5cf6, 阅读 #ef4444]`，`tags=[.NET]`。按键用 `new KeyboardEvent('keydown', { key, bubbles: true })` 派发到输入框上。

```ts
it('opens a combobox listing tags not yet on the article, loading only on open')
//   挂载时 getTags 没有被调用；点击「添加标签」后调用 1 次
//   [role="combobox"] 获得焦点，aria-expanded="true"，placeholder 为 "搜索或新建标签…"，maxLength === 100
//   option 文本依次为 [Go, Gopher, 阅读]

it('selecting an existing option adds it by its own name and keeps the form open for the next tag')
//   点击 option "Go" → addTag('article', { name:'Go', color:'#3b82f6' })；onTagsChange 收到 [.NET, 返回的标签]；
//   输入框为空且仍有焦点；「完成」按钮存在；「添加标签」按钮没有出现

it('Enter with no highlight adds the case-insensitive exact match, not a partial match')
//   输入 " go "，按 Enter → addTag 的 name 为 'Go'；没有出现 "创建「go」"；没有 legend 为 "新标签颜色" 的元素

it('arrow keys highlight options and Enter selects the highlighted one')
//   输入 "go"，按两次 ArrowDown → aria-activedescendant 指向 "Gopher" 选项，且它的 aria-selected="true"
//   按 Enter → addTag 的 name 为 'Gopher'

it('offers 创建「X」 with a color for a new name and creates it')
//   输入 "Rust" → 出现 "没有匹配的标签"、选项 "创建「Rust」" 和 legend "新标签颜色"
//   点击 "选择颜色 #ef4444"，按 Enter → addTag({ name:'Rust', color:'#ef4444' })
//   成功后输入为空，"选择颜色 #3b82f6" 的 aria-pressed="true"

it('shows 已添加 and ignores Enter for a tag already on the article')
//   输入 ".net" → 出现 "已添加"；按 Enter 后 addTag 没有被调用；「添加」disabled

it('still creates when tags fail to load')
//   getTags reject → 出现 "标签加载失败"；输入 "New"，按 Enter → addTag({ name:'New', color:'#3b82f6' })

it('a tag removed while the form is open returns to the list')
//   用一个 Harness 把 tags 放进 state，onTagsChange 时更新 state
//   展开表单后点击「移除 .NET 标签」→ 选项中出现 ".NET"

it('adds once while a request is pending and keeps the input on failure')
//   addTag 先返回一个手动控制的 Promise；输入 "Rust" 后按两次 Enter → addTag 只被调用 1 次；输入框 disabled
//   让它 reject → show 以 "添加标签失败，请重试" 调用；输入框的值仍是 "Rust"

it('Escape closes the form')
//   在输入框上按 Escape → combobox 消失，「添加标签」按钮重新出现
```

- [ ] **Step 2: 运行测试，确认失败**

Run：`pnpm test -- src/lib/tag-suggestions.test.ts "src/app/pages/article/[id]/components/TagManager.test.tsx"`
Expected：`tag-suggestions` 失败，提示模块不存在；`TagManager` 的新用例失败，提示找不到 combobox，原有用例仍然通过。

- [ ] **Step 3: 实现 `tagSuggestions`**

按上面 Interfaces 中的规则实现，不需要另外的算法。

- [ ] **Step 4: 改写 `TagManager` 的加标签表单**

**状态**
- 保留 `showAddForm`、`isAdding`；
- `newTagName` 改名为 `query`，`newTagColor` 改名为 `color`，初始值都改用 `DEFAULT_TAG_COLOR`；
- 新增 `allTags: Tag[]`、`loadState: 'loading' | 'ready' | 'error'`、`activeIndex: number`（-1 表示没有高亮）；
- `const s = tagSuggestions(allTags, tags, query)`，每次渲染都重新计算，**不要缓存**。

**加载**
- 展开时设置 `loadState='loading'` 并调用 `tagApi.getTags()`；
- 用一个 `active` 标志，防止组件卸载或表单收起后再写状态。

**可选项**
- 可选项由已有标签和一个可选的新建项组成。新建项使用 `id: null`，↑↓ 在这个范围内移动 `activeIndex`；
- 修改输入内容时把 `activeIndex` 重置为 -1。

**提交**（`submit()`，Enter 和「添加」按钮共用）
- 已有标签仍在加载或请求正在提交时，暂不接受提交。
- Enter 优先选择显式高亮的项。「添加」使用默认项。
- 没有可用的高亮项时，已加过的同名标签拦截默认提交，其余情况优先使用完整匹配，其次使用新建项。

**添加**（`add(name, color)`）
- 调用 `articleApi.addTag`；
- 成功后：调用 `onTagsChange([...tags, created])`；如果 `allTags` 中还没有 `created.id`，就加进去；重置 `query`、`color`、`activeIndex`；让输入框重新获得焦点；
- 失败时调用 `toast.show("添加标签失败，请重试")`。

**结构**（在 `addForm` 中）
- 输入框为 `Input`，属性包括 `role="combobox"`、`aria-autocomplete="list"`、`aria-expanded`、`aria-controls={listId}`、`aria-activedescendant`；
- 下面是 `<ul role="listbox" id={listId}>`，放在文档流中，不使用绝对定位，避免被卡片的 overflow 裁掉；
- 每个 `<li role="option" id aria-selected>` 带 `onMouseDown={e => e.preventDefault()}`，保证点击时焦点留在输入框；
- 状态提示行（「正在加载标签…」「标签加载失败」「没有匹配的标签」「已添加」）作为 `<li role="presentation">` 放进列表；
- 只有 `s.canCreate` 时才渲染 `<fieldset><legend>新标签颜色</legend>…</fieldset>`，颜色按钮和「自定义」与现在相同；
- 按钮为「添加」/「添加中…」，以及「完成」。

**键盘**
- Escape 只在输入框的 `onKeyDown` 中处理，并调用 `stopPropagation`，以免触发阅读页上其他的 Esc 处理。
- 输入法组字期间，回车用于确认输入。

- [ ] **Step 5: 样式 `TagManager.module.css`**

- `.listbox`：`margin: var(--space-1) 0 0; padding: 4px; list-style: none; max-height: 240px; overflow: auto`；边框、圆角、背景与 `.tag` 的配色一致；`max-width: 100%`。
- `.option`：`display:flex; align-items:center; gap: var(--space-2); padding: 6px 8px; border-radius: var(--radius-control); cursor: pointer; overflow-wrap:anywhere`。
- `.option[aria-selected="true"]` 和 `.option:hover`：`background: var(--surface-muted)`。
- `.createOption`：用强调色区分。
- `@media (pointer: coarse), (max-width: 600px)`：`.option { min-height: 44px; }`。

- [ ] **Step 6: 运行测试，确认通过**

1. Run：与 Step 2 相同。Expected：全部 PASS。
2. Run：`pnpm test`。Expected：失败数为 0，包括 `ArticleCard.test.tsx`、`Client.test.tsx`、`page.batch.test.tsx`。

- [ ] **Step 7: 更新 `CLAUDE.md` 的 Frontend Features**

在 Localization 条目后面加入：

```
- **Tag Management**: `/pages/tags` creates tags through an Arc Dialog (name 1–100, 10 shared colors in `lib/tag-colors.ts`; 409 shows 已有同名标签) and deletes from each card after confirmation; deletion calls `notifyLibraryMutation()` so library caches and feeds drop the tag. The reader/card `TagManager` is an ARIA combobox over existing tags (case-insensitive match via `lib/tag-suggestions.ts`); an unmatched name offers 创建「X」 with a color, and the form stays open for the next tag.
```

- [ ] **Step 8: 提交**

```bash
git add src/frontend/src/lib/tag-suggestions.ts src/frontend/src/lib/tag-suggestions.test.ts "src/frontend/src/app/pages/article/[id]/components/TagManager.tsx" "src/frontend/src/app/pages/article/[id]/components/TagManager.module.css" "src/frontend/src/app/pages/article/[id]/components/TagManager.test.tsx" CLAUDE.md
git commit -m "feat(web): pick existing tags or create new ones when tagging"
```

---

### Task 5: 整体验证与浏览器验收

**Files:** 这个任务不修改代码。如果发现问题，回到对应的 Task 修复，并补上能复现问题的测试。

- [ ] **Step 1: 运行全部测试和检查**

```bash
dotnet test                                   # Expected: 0 failed
cd src/frontend && pnpm test                  # Expected: 0 failed
pnpm exec tsc --noEmit                        # Expected: exit 0
pnpm exec eslint src --ignore-pattern src/app/pages/account/page.tsx   # Expected: 只剩现有的 ArticleCard <img> 警告
```

- [x] **Step 2: 浏览器验收（1280×800 与 375×812）**

先启动后端（`dotnet run`）和前端（`pnpm dev`），再用 ego-browser 技能逐项检查。

**标签页 `http://localhost:4000/pages/tags`**
1. 新建「验收 <时间戳>」：它出现在列表中，显示「0 篇」。
2. 用相同名称再建一次：对话框中显示「已有同名标签」，对话框不关闭。

**阅读页**
3. 打开一篇文章，点击「添加标签」：下拉列表中有第 1 步建的标签。选中它后，标签出现在文章上，颜色与建的时候相同；回到标签页，它显示「1 篇」，没有重复的标签。
4. 输入一个不存在的名称，选「创建「X」」并换一种颜色：新标签带着所选颜色加到文章上。
5. 输入第 1 步标签名称的不同大小写形式，按回车：加上的是已有的那个标签，不会新建。

**文库**
6. 在文库卡片上展开加标签：行为与阅读页相同。

**删除**
7. 在标签页删除第 1 步的标签：确认文字中包含「1 篇文章」。删除后，文库卡片和阅读页都不再显示它。

**手机宽度（375）**
8. 不悬停也能看到删除按钮，并且能点击。
9. 阅读页的下拉列表不会产生横向滚动：`document.documentElement.scrollWidth <= innerWidth`。

验收完成后，删除验收时建的所有标签，并确认原有的 65 篇文章不受影响。

### 验收记录（2026-11-14）

审查修订后使用 ego-browser 在独立 PostgreSQL 数据库副本复验。前端监听 `http://localhost:4100`，后端运行 .NET 9.0.23。

- Chromium 1280×800。标签列表创建后显示「0 篇」并恢复焦点，重名提示保留输入。阅读页选择已有标签时保留颜色，从阅读页新建可指定颜色。输入与已加标签同名的 `.net` 时，直接回车无操作，方向键高亮 `.NET Core` 候选项后回车成功添加该项。文库卡片输入不同大小写仍命中同一标签 ID。删除后重新进入文库、阅读页和订阅页，标签已消失。
- Chromium 375×812，启用触屏模拟。删除按钮无悬停时可见，实测 44×44 CSS 像素，确认提示显示正确的「1 篇文章」。新建对话框正常显示 10 种预设颜色。阅读页输入 100 字符后，页面宽度为 375 像素，下拉列表可视宽度和滚动宽度均为 307 像素。
- 无悬停、精细指针的样式分支。在 1280×800 的精细指针视口单独激活该 CSS 规则，按钮由 32×32 变为 44×44，完全可见。该项为浏览器样式检查。
- 清理所有验收标签和临时订阅。逐行比较确认 65 篇正常文章及原有 35 条文章标签关联与验收前一致。
- `dotnet test` 398/398，通过真实 PostgreSQL 并发测试。`pnpm test --maxWorkers=2` 167/167，TypeScript 检查通过，ESLint 仅报告既有的 `ArticleCard` 图片警告。生成的 OpenAPI 文档包含 201、400、409 响应体，实际 201 的 `Location` 为小写详情路径。

### 上轮审查复验

- 后端在 .NET 9.0.23 和真实 PostgreSQL 上通过 398/398，包括由 Unit of Work 保存时命中唯一索引的两个并发请求。`POST /api/tags` 的 OpenAPI 仍包含 201、400、409，201 的 `Location` 仍是小写详情路径。
- 分别构建 `HEAD`（`d9122fa`）和当时工作区的 `net9.0` 测试程序集，并在两个运行时上执行阅读偏好登录用例。两份程序集在 .NET 9.0.23 上均通过，在系统 .NET 10.0.12 配合 `DOTNET_ROLL_FORWARD=Major` 时均在首次请求收到 401，测试期望 302。
- 前端 29 个文件共 167/167，TypeScript 通过，限定范围的 ESLint 仅报告既有的 `ArticleCard` 图片警告。
- ego-browser 在 1280×800 复验新建、重名、高亮项回车选择、文库卡片大小写匹配和删除刷新。在 375×812 触屏视口下，标签列表创建了一个 100 字符的标签，页面的 `scrollWidth` 为 375，删除按钮可见且为 44×44。阅读页宽度为 375，下拉列表可视与滚动宽度均为 307。桌面精细指针下单独激活无悬停尺寸规则，按钮为 44×44。
- 全部验收标签已清理，65 篇正常文章及原有 35 条文章标签关联与验收前逐行一致。

### 本轮复验

在隔离的 PostgreSQL 副本上使用 ego-browser 复验 Chromium 375×812 触屏视口。

- 标签列表页用对话框创建红色标签，卡片显示「0 篇」。再次提交同名时显示「已有同名标签」，保留对话框和输入。页面宽度为 375 CSS 像素，没有横向滚动。删除按钮无需悬停即可看到，点击区域为 44×44 CSS 像素。
- 阅读页以小写输入筛选，并点选保留原名与颜色的已有标签。随后选绿色新建一个标签。返回标签页后，两张卡片均显示「1 篇」，阅读页新建的标签颜色正确。阅读页宽度为 375 像素，下拉列表可视宽度和滚动宽度均为 307 像素。
- 文库卡片输入同一已有标签的小写名称并按回车，使用的是原标签 ID，标签页接口计数更新为 2。随后从卡片新建一个紫色标签，文章计数为 1。文库页面宽度为 375 像素，卡片下拉列表可视宽度和滚动宽度均为 273 像素。
- 从标签页删除上述三个验收标签。删除确认依次显示「2 篇文章」「1 篇文章」「1 篇文章」，删除后在阅读页和文库卡片均已消失。逐行比较确认 65 篇正常文章和原有 35 条文章标签关联与验收前一致。
- 在目标仍为 `net9.0` 时用 .NET 9.0.23 执行后端全量测试，398/398 通过。真实 PostgreSQL 并发创建返回一个 201 和一个 409，响应中的已有标签 ID 对应同一条记录。OpenAPI 的 201、400、409 响应体以及 201 的小写 `Location` 均已核对。
- 前端 29 个文件共 167/167。TypeScript 检查通过，限定范围的 ESLint 仅报告既有的 `ArticleCard` 图片警告。
- 验收期间工作区中的两个项目改为 `net10.0`。直接按该目标编译和运行后端全量测试，396/398 通过，标签相关 39 个定向用例全通过。阅读偏好登录用例收到 401 而期望 302，文库组合筛选用例在 EF Core 求值阅读状态表达式时报 `ReadOnlySpan<ReadingStatus>` 异常。从 `d9122fa` 导出副本，仅把两个项目的目标框架改为 `net10.0`，同样复现这两项失败。筛选异常与 C# 14 对数组 `Contains` 的重载解析变化吻合。[Microsoft 的兼容性说明](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/csharp-overload-resolution)
