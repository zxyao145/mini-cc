"use client";

import { useState, useEffect } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, accessKeyApi } from "@/lib/api";
import { ApiKey, CreateApiKeyRequest, UpdateApiKeyRequest } from "@/types";
import styles from "./account.module.scss";

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

export default function AccountPage() {
  const { user, refreshUser } = useAuth();
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
  const [editingKey, setEditingKey] = useState<ApiKey | null>(null);

  // Messages
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  useEffect(() => {
    if (user) {
      setUserForm({ userName: user.userName });
    }
  }, [user]);

  useEffect(() => {
    if (activeTab === "accessKeys") {
      loadApiKeys();
    }
  }, [activeTab]);

  const showMessage = (type: "success" | "error", text: string) => {
    setMessage({ type, text });
    setTimeout(() => setMessage(null), 3000);
  };

  const loadApiKeys = async () => {
    try {
      setIsLoadingKeys(true);
      const keys = await accessKeyApi.getApiKeys();
      setApiKeys(keys);
    } catch (error) {
      showMessage("error", "加载 Api Keys 失败");
    } finally {
      setIsLoadingKeys(false);
    }
  };

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
    } catch (error: any) {
      showMessage("error", error.response?.data?.message || "用户名更新失败");
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
    } catch (error: any) {
      showMessage("error", error.response?.data?.message || "密码更新失败");
    } finally {
      setIsUpdatingProfile(false);
    }
  };

  const handleCreateApiKey = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!keyForm.name.trim()) {
      showMessage("error", "请输入 Api Key 名称");
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
      showMessage("success", "Api Key 创建成功");
    } catch (error: any) {
      showMessage("error", error.response?.data?.message || "Api Key 创建失败");
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
      showMessage("success", disabled ? "Api Key 已禁用" : "Api Key 已启用");
    } catch (error: any) {
      showMessage("error", error.response?.data?.message || "Api Key 更新失败");
    }
  };

  const handleDeleteApiKey = async (id: string) => {
    if (!confirm("确定要删除这个 Api Key 吗？")) {
      return;
    }
    
    try {
      await accessKeyApi.deleteApiKey(id);
      await loadApiKeys();
      showMessage("success", "Api Key 删除成功");
    } catch (error: any) {
      showMessage("error", error.response?.data?.message || "Api Key 删除失败");
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
      <div className={styles.header}>
        <h1>账户设置</h1>
      </div>

      {message && (
        <div className={`${styles.message} ${styles[message.type]}`}>
          {message.text}
        </div>
      )}

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === "profile" ? styles.active : ""}`}
          onClick={() => setActiveTab("profile")}
        >
          个人资料
        </button>
        <button
          className={`${styles.tab} ${activeTab === "accessKeys" ? styles.active : ""}`}
          onClick={() => setActiveTab("accessKeys")}
        >
          Api Keys
        </button>
      </div>

      {activeTab === "profile" && (
        <div className={styles.section}>
          <h2>个人资料</h2>
          
          <form onSubmit={handleUpdateUserName} className={styles.form}>
            <div className={styles.field}>
              <label htmlFor="userName">用户名</label>
              <input
                id="userName"
                type="text"
                value={userForm.userName}
                onChange={(e) => setUserForm({ ...userForm, userName: e.target.value })}
                minLength={2}
                maxLength={20}
                required
              />
              <small>长度限制：2-20字符</small>
            </div>
            <button type="submit" disabled={isUpdatingProfile} className={styles.button}>
              {isUpdatingProfile ? "更新中..." : "更新用户名"}
            </button>
          </form>

          <form onSubmit={handleUpdatePassword} className={styles.form}>
            <h3>修改密码</h3>
            <div className={styles.field}>
              <label htmlFor="currentPassword">当前密码</label>
              <input
                id="currentPassword"
                type="password"
                value={passwordForm.currentPassword}
                onChange={(e) => setPasswordForm({ ...passwordForm, currentPassword: e.target.value })}
                required
              />
            </div>
            <div className={styles.field}>
              <label htmlFor="newPassword">新密码</label>
              <input
                id="newPassword"
                type="password"
                value={passwordForm.newPassword}
                onChange={(e) => setPasswordForm({ ...passwordForm, newPassword: e.target.value })}
                minLength={6}
                maxLength={20}
                required
              />
              <small>长度限制：6-20字符</small>
            </div>
            <div className={styles.field}>
              <label htmlFor="confirmPassword">确认新密码</label>
              <input
                id="confirmPassword"
                type="password"
                value={passwordForm.confirmPassword}
                onChange={(e) => setPasswordForm({ ...passwordForm, confirmPassword: e.target.value })}
                minLength={6}
                maxLength={20}
                required
              />
            </div>
            <button type="submit" disabled={isUpdatingProfile} className={styles.button}>
              {isUpdatingProfile ? "更新中..." : "更新密码"}
            </button>
          </form>
        </div>
      )}

      {activeTab === "accessKeys" && (
        <div className={styles.section}>
          <div className={styles.sectionHeader}>
            <h2>Api Keys</h2>
            <button
              onClick={() => setShowCreateKeyForm(true)}
              className={styles.button}
              disabled={apiKeys.length >= 10}
            >
              新建 Api Key
            </button>
          </div>
          
          <p className={styles.hint}>
            Api Keys 用于 API 调用和浏览器扩展。最多可创建 10 个。
          </p>

          {showCreateKeyForm && (
            <form onSubmit={handleCreateApiKey} className={styles.form}>
              <h3>创建新的 Api Key</h3>
              <div className={styles.field}>
                <label htmlFor="keyName">名称</label>
                <input
                  id="keyName"
                  type="text"
                  value={keyForm.name}
                  onChange={(e) => setKeyForm({ ...keyForm, name: e.target.value })}
                  placeholder="输入 Api Key 名称"
                  maxLength={50}
                  required
                />
              </div>
              <div className={styles.field}>
                <label htmlFor="expiredTime">过期时间（可选）</label>
                <input
                  id="expiredTime"
                  type="datetime-local"
                  value={keyForm.expiredTime}
                  onChange={(e) => setKeyForm({ ...keyForm, expiredTime: e.target.value })}
                />
              </div>
              <div className={styles.formActions}>
                <button type="submit" disabled={isCreatingKey} className={styles.button}>
                  {isCreatingKey ? "创建中..." : "创建"}
                </button>
                <button
                  type="button"
                  onClick={() => setShowCreateKeyForm(false)}
                  className={styles.buttonSecondary}
                >
                  取消
                </button>
              </div>
            </form>
          )}

          {isLoadingKeys ? (
            <div className={styles.loading}>加载中...</div>
          ) : (
            <div className={styles.keyList}>
              {apiKeys.length === 0 ? (
                <div className={styles.empty}>暂无 Api Keys</div>
              ) : (
                apiKeys.map((key) => (
                  <div key={key.id} className={styles.keyItem}>
                    <div className={styles.keyInfo}>
                      <h4>{key.name}</h4>
                      <div className={styles.keyValue}>
                        <code>{key.key}</code>
                        <button
                          onClick={() => copyToClipboard(key.key)}
                          className={styles.copyButton}
                        >
                          复制
                        </button>
                      </div>
                      <div className={styles.keyMeta}>
                        {key.expiredTime && (
                          <span>过期时间: {new Date(key.expiredTime).toLocaleString()}</span>
                        )}
                        <span className={`${styles.status} ${key.disabled ? styles.disabled : styles.enabled}`}>
                          {key.disabled ? "已禁用" : "已启用"}
                        </span>
                      </div>
                    </div>
                    <div className={styles.keyActions}>
                      <button
                        onClick={() => handleUpdateApiKey(key, !key.disabled)}
                        className={styles.buttonSecondary}
                      >
                        {key.disabled ? "启用" : "禁用"}
                      </button>
                      <button
                        onClick={() => handleDeleteApiKey(key.id)}
                        className={styles.buttonDanger}
                      >
                        删除
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}