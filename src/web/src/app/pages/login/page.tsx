'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { BookOpen } from 'lucide-react';
import { Button } from '@/components/arc/button/button';
import { Checkbox } from '@/components/arc/checkbox/checkbox';
import { Input } from '@/components/arc/input/input';
import { useAuth } from '@/contexts/AuthContext';
import styles from './login.module.css';

export default function LoginPage() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  
  const { login } = useAuth();
  const router = useRouter();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userName || !password) {
      setError('请输入用户名和密码');
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      await login(userName, password, rememberMe);
      router.push('/');
    } catch {
      setError('用户名或密码错误');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className={styles.container}>
      <section className={styles.loginBox} aria-labelledby="login-title">
        <div className={styles.brand}>
          <span className={styles.brandMark}><BookOpen size={20} strokeWidth={1.8} aria-hidden="true" /></span>
          <span>MiniCc<span className={styles.brandDot}>.</span></span>
        </div>
        <h1 className={styles.title} id="login-title">登录</h1>

        <form onSubmit={handleSubmit} className={styles.form}>
          <Input
            label="用户名"
            id="userName"
            value={userName}
            onChange={event => setUserName(event.target.value)}
            placeholder="请输入用户名"
            autoComplete="username"
            disabled={isLoading}
          />
          <Input
            label="密码"
            id="password"
            type="password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            placeholder="请输入密码"
            autoComplete="current-password"
            disabled={isLoading}
          />

          <Checkbox
            label="记住我"
            id="rememberMe"
            checked={rememberMe}
            onCheckedChange={checked => setRememberMe(checked === true)}
            disabled={isLoading}
          />

          {error && <p className={styles.error} role="alert">{error}</p>}

          <Button type="submit" className={styles.submitButton} disabled={isLoading} loading={isLoading}>
            {isLoading ? '登录中…' : '登录'}
          </Button>
        </form>
      </section>
    </div>
  );
}
