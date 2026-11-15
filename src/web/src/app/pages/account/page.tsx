"use client";

import { useState, useEffect, useCallback } from "react";
import axios from "axios";
import { Button } from "@/components/arc/button/button";
import { Input } from "@/components/arc/input/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/arc/tabs/tabs";
import { useConfirm } from "@/contexts/ConfirmContext";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, accessKeyApi } from "@/lib/api";
import { ApiKey, CreateApiKeyRequest, UpdateApiKeyRequest } from "@/types";
import styles from "./account.module.css";

interface UserUpdateForm {
  userName: string;
}

interface PasswordUpdateForm {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

interface ApiKeyForm {
  name: string;
  expiredTime: string;
}

function getErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError<{ message?: string }>(error)) {
    return error.response?.data?.message || fallback;
  }
  return fallback;
}

export default function AccountPage() {
  const { user, refreshUser } = useAuth();
  const confirm = useConfirm();
  const [activeTab, setActiveTab] = useState<"profile" | "accessKeys">("profile");

  // Profile update states
  const [userForm, setUserForm] = useState<UserUpdateForm>({ userName: "" });
  const [passwordForm, setPasswordForm] = useState<PasswordUpdateForm>({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });
  const [isUpdatingProfile, setIsUpdatingProfile] = useState(false);

  // Api Keys states
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([]);
  const [isLoadingKeys, setIsLoadingKeys] = useState(true);
  const [showCreateKeyForm, setShowCreateKeyForm] = useState(false);
  const [keyForm, setKeyForm] = useState<ApiKeyForm>({ name: "", expiredTime: "" });
  const [isCreatingKey, setIsCreatingKey] = useState(false);
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- Existing state kept during the UI redesign.
  const [editingKey, setEditingKey] = useState<ApiKey | null>(null);

  // Messages
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  useEffect(() => {
    if (user) {
      setUserForm({ userName: user.userName });
    }
  }, [user]);

  const showMessage = useCallback((type: "success" | "error", text: string) => {
    setMessage({ type, text });
    setTimeout(() => setMessage(null), 3000);
  }, []);

  const loadApiKeys = useCallback(async () => {
    try {
      setIsLoadingKeys(true);
      const keys = await accessKeyApi.getApiKeys();
      setApiKeys(keys);
    } catch {
      showMessage("error", "加载 API 密钥失败");
    } finally {
      setIsLoadingKeys(false);
    }
  }, [showMessage]);

  useEffect(() => {
    if (activeTab === "accessKeys") {
      loadApiKeys();
    }
  }, [activeTab, loadApiKeys]);

  const handleUpdateUserName = async (e: React.FormEvent) => {
    e.preventDefault();
    if (userForm.userName.length < 2 || userForm.userName.length > 20) {
      showMessage("error", "用户名长度必须在2-20字符之间");
      return;
    }

    try {
      setIsUpdatingProfile(true);
      await authApi.updateUserName({ userName: userForm.userName });
      await refreshUser();
      showMessage("success", "用户名更新成功");
    } catch (error: unknown) {
      showMessage("error", getErrorMessage(error, "用户名更新失败"));
    } finally {
      setIsUpdatingProfile(false);
    }
  };

  const handleUpdatePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (passwordForm.newPassword.length < 6 || passwordForm.newPassword.length > 20) {
      showMessage("error", "密码长度必须在6-20字符之间");
      return;
    }
    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      showMessage("error", "新密码确认不一致");
      return;
    }

    try {
      setIsUpdatingProfile(true);
      await authApi.updatePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
      });
      setPasswordForm({ currentPassword: "", newPassword: "", confirmPassword: "" });
      showMessage("success", "密码更新成功");
    } catch (error: unknown) {
      showMessage("error", getErrorMessage(error, "密码更新失败"));
    } finally {
      setIsUpdatingProfile(false);
    }
  };

  const handleCreateApiKey = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!keyForm.name.trim()) {
      showMessage("error", "请输入 API 密钥名称");
      return;
    }

    try {
      setIsCreatingKey(true);
      const request: CreateApiKeyRequest = {
        name: keyForm.name,
        expiredTime: keyForm.expiredTime || undefined,
      };
      await accessKeyApi.createApiKey(request);
      setKeyForm({ name: "", expiredTime: "" });
      setShowCreateKeyForm(false);
      await loadApiKeys();
      showMessage("success", "API 密钥创建成功");
    } catch (error: unknown) {
      showMessage("error", getErrorMessage(error, "API 密钥创建失败"));
    } finally {
      setIsCreatingKey(false);
    }
  };

  const handleUpdateApiKey = async (key: ApiKey, disabled: boolean) => {
    try {
      const request: UpdateApiKeyRequest = {
        id: key.id,
        name: key.name,
        expiredTime: key.expiredTime,
        disabled,
      };
      await accessKeyApi.updateApiKey(request);
      await loadApiKeys();
      showMessage("success", disabled ? "API 密钥已禁用" : "API 密钥已启用");
    } catch (error: unknown) {
      showMessage("error", getErrorMessage(error, "API 密钥更新失败"));
    }
  };

  const handleDeleteApiKey = async (id: string) => {
    if (!(await confirm("确定要删除这个 API 密钥吗？", { title: "删除 API 密钥", confirmLabel: "删除" }))) {
      return;
    }

    try {
      await accessKeyApi.deleteApiKey(id);
      await loadApiKeys();
      showMessage("success", "API 密钥删除成功");
    } catch (error: unknown) {
      showMessage("error", getErrorMessage(error, "API 密钥删除失败"));
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    showMessage("success", "已复制到剪贴板");
  };

  if (!user?.isAuthenticated) {
    return <div className={styles.container}>请先登录</div>;
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1>账户设置</h1>
      </header>

      {message && <p className={`${styles.message} ${styles[message.type]}`} role={message.type === "error" ? "alert" : "status"}>{message.text}</p>}

      <Tabs value={activeTab} onValueChange={value => setActiveTab(value as "profile" | "accessKeys")} className={styles.tabs}>
        <TabsList className={styles.tabList} aria-label="账户设置">
          <TabsTrigger className={styles.tab} value="profile">个人资料</TabsTrigger>
          <TabsTrigger className={styles.tab} value="accessKeys">API 密钥</TabsTrigger>
        </TabsList>

        <TabsContent className={styles.section} value="profile">
          <div className={styles.sectionHeading}>
            <div>
              <h2>个人资料</h2>
              <p>更新用户名或修改账户密码。</p>
            </div>
          </div>

          <form onSubmit={handleUpdateUserName} className={styles.form}>
            <Input
              id="userName"
              label="用户名"
              value={userForm.userName}
              onChange={event => setUserForm({ userName: event.target.value })}
              minLength={2}
              maxLength={20}
              required
              description="长度为 2–20 个字符。"
            />
            <Button type="submit" disabled={isUpdatingProfile}>{isUpdatingProfile ? "更新中…" : "更新用户名"}</Button>
          </form>

          <form onSubmit={handleUpdatePassword} className={`${styles.form} ${styles.passwordForm}`}>
            <h3>修改密码</h3>
            <Input
              id="currentPassword"
              label="当前密码"
              type="password"
              autoComplete="current-password"
              value={passwordForm.currentPassword}
              onChange={event => setPasswordForm({ ...passwordForm, currentPassword: event.target.value })}
              required
            />
            <Input
              id="newPassword"
              label="新密码"
              type="password"
              autoComplete="new-password"
              value={passwordForm.newPassword}
              onChange={event => setPasswordForm({ ...passwordForm, newPassword: event.target.value })}
              minLength={6}
              maxLength={20}
              required
              description="长度为 6–20 个字符。"
            />
            <Input
              id="confirmPassword"
              label="确认新密码"
              type="password"
              autoComplete="new-password"
              value={passwordForm.confirmPassword}
              onChange={event => setPasswordForm({ ...passwordForm, confirmPassword: event.target.value })}
              minLength={6}
              maxLength={20}
              required
            />
            <Button type="submit" disabled={isUpdatingProfile}>{isUpdatingProfile ? "更新中…" : "更新密码"}</Button>
          </form>
        </TabsContent>

        <TabsContent className={styles.section} value="accessKeys">
          <div className={styles.sectionHeading}>
            <div>
              <h2>API 密钥</h2>
              <p>用于 API 调用和浏览器扩展，最多可创建 10 个。</p>
            </div>
            <Button onClick={() => setShowCreateKeyForm(true)} disabled={apiKeys.length >= 10}>新建密钥</Button>
          </div>

          {showCreateKeyForm && (
            <form onSubmit={handleCreateApiKey} className={`${styles.form} ${styles.createForm}`}>
              <h3>创建 API 密钥</h3>
              <Input
                id="keyName"
                label="名称"
                value={keyForm.name}
                onChange={event => setKeyForm({ ...keyForm, name: event.target.value })}
                placeholder="输入密钥名称"
                maxLength={50}
                required
              />
              <Input
                id="expiredTime"
                label="过期时间（可选）"
                type="datetime-local"
                value={keyForm.expiredTime}
                onChange={event => setKeyForm({ ...keyForm, expiredTime: event.target.value })}
              />
              <div className={styles.formActions}>
                <Button type="submit" disabled={isCreatingKey}>{isCreatingKey ? "创建中…" : "创建"}</Button>
                <Button type="button" variant="secondary" onClick={() => setShowCreateKeyForm(false)}>取消</Button>
              </div>
            </form>
          )}

          {isLoadingKeys ? (
            <p className={styles.loading} role="status">加载中…</p>
          ) : apiKeys.length === 0 ? (
            <p className={styles.empty}>暂无 API 密钥</p>
          ) : (
            <div className={styles.keyList}>
              {apiKeys.map(key => (
                <article key={key.id} className={styles.keyItem}>
                  <div className={styles.keyInfo}>
                    <h3>{key.name}</h3>
                    <div className={styles.keyValue}>
                      <code>{key.key}</code>
                      <Button size="sm" variant="ghost" onClick={() => copyToClipboard(key.key)}>复制</Button>
                    </div>
                    <div className={styles.keyMeta}>
                      {key.expiredTime && <span>过期时间：{new Date(key.expiredTime).toLocaleString("zh-CN")}</span>}
                      <span className={`${styles.status} ${key.disabled ? styles.disabled : styles.enabled}`}>
                        {key.disabled ? "已停用" : "已启用"}
                      </span>
                    </div>
                  </div>
                  <div className={styles.keyActions}>
                    <Button size="sm" variant="secondary" onClick={() => void handleUpdateApiKey(key, !key.disabled)}>{key.disabled ? "启用" : "停用"}</Button>
                    <Button size="sm" variant="danger" onClick={() => void handleDeleteApiKey(key.id)}>删除</Button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
