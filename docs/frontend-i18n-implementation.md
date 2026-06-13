# 前端多语言实现方案

## 🎯 整体架构

```
┌─────────────────────────────────────────────────────────────┐
│                        前端应用                               │
├─────────────────────────────────────────────────────────────┤
│  1. I18nService (多语言服务)                                  │
│     - loadLanguagePack()    - 从后端加载语言包               │
│     - setLanguage()         - 切换语言                       │
│     - t(key, module)        - 翻译函数                       │
│     - getLanguage()         - 获取当前语言                   │
├─────────────────────────────────────────────────────────────┤
│  2. LocalStorage (本地缓存)                                  │
│     - language_pack_{lang}  - 语言包数据                     │
│     - current_language      - 当前语言                       │
│     - language_version      - 版本号（用于更新检测）         │
├─────────────────────────────────────────────────────────────┤
│  3. Components (组件)                                         │
│     - LanguageSwitcher      - 语言切换器                     │
│     - I18nProvider          - 上下文提供者（React）          │
│     - v-i18n directive      - Vue 指令                      │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    后端 API                                   │
│  GET /api/v1/Sys_Language/LanguagePack?lang=zh&module=System│
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 实现方案 1：Vue.js + Composition API

### 1.1 创建 i18n 服务

```javascript
// src/services/i18n.js
import { ref, computed } from 'vue'
import axios from 'axios'

const API_BASE = 'http://localhost:51479/api/v1'

// 响应式状态
const currentLanguage = ref(localStorage.getItem('current_language') || 'zh')
const languagePacks = ref({})
const loading = ref(false)
const error = ref(null)

export function useI18n() {
  /**
   * 加载语言包
   */
  const loadLanguagePack = async (lang, module = null) => {
    // 检查本地缓存
    const cacheKey = module
      ? `language_pack_${lang}_${module}`
      : `language_pack_${lang}`

    const cachedData = localStorage.getItem(cacheKey)
    const cachedVersion = localStorage.getItem(`language_version_${lang}`)

    // 如果有缓存且版本未过期，直接使用
    if (cachedData && cachedVersion) {
      try {
        const { version, data } = JSON.parse(cachedData)
        // 缓存有效期：1天
        const cacheTime = localStorage.getItem(`language_cache_time_${lang}`)
        const isExpired = !cacheTime || (Date.now() - parseInt(cacheTime)) > 24 * 60 * 60 * 1000

        if (!isExpired) {
          languagePacks.value[lang] = data
          return data
        }
      } catch (e) {
        console.warn('解析缓存失败:', e)
      }
    }

    // 从后端加载
    loading.value = true
    error.value = null

    try {
      const response = await axios.get(`${API_BASE}/Sys_Language/LanguagePack`, {
        params: { lang, module }
      })

      if (response.data.status) {
        const pack = response.data.data
        languagePacks.value[lang] = pack

        // 缓存到本地
        localStorage.setItem(cacheKey, JSON.stringify({
          version: pack.version,
          data: pack,
          timestamp: Date.now()
        }))
        localStorage.setItem(`language_version_${lang}`, pack.version)
        localStorage.setItem(`language_cache_time_${lang}`, Date.now().toString())

        return pack
      } else {
        throw new Error(response.data.message)
      }
    } catch (err) {
      error.value = err.message
      console.error('加载语言包失败:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  /**
   * 切换语言
   */
  const setLanguage = async (lang) => {
    if (!languagePacks.value[lang]) {
      await loadLanguagePack(lang)
    }
    currentLanguage.value = lang
    localStorage.setItem('current_language', lang)
    document.documentElement.lang = lang
  }

  /**
   * 翻译函数
   */
  const t = (key, module = null, defaultValue = key) => {
    const lang = currentLanguage.value
    const pack = languagePacks.value[lang]

    if (!pack) {
      console.warn(`语言包 ${lang} 未加载`)
      return defaultValue
    }

    // 优先从模块中查找
    if (module && pack.modules && pack.modules[module]) {
      const moduleValue = pack.modules[module][key]
      if (moduleValue) return moduleValue
    }

    // 从扁平结构中查找
    if (pack.flat && pack.flat[key]) {
      return pack.flat[key]
    }

    return defaultValue
  }

  /**
   * 获取当前语言
   */
  const getLanguage = () => currentLanguage.value

  /**
   * 获取可用语言列表
   */
  const getAvailableLanguages = () => [
    { code: 'zh', name: '中文', icon: '🇨🇳' },
    { code: 'en', name: 'English', icon: '🇺🇸' },
    { code: 'de', name: 'Deutsch', icon: '🇩🇪' },
    { code: 'id', name: 'Indonesian', icon: '🇮🇩' }
  ]

  /**
   * 初始化（应用启动时调用）
   */
  const init = async () => {
    await loadLanguagePack(currentLanguage.value)
  }

  return {
    // 状态
    currentLanguage: computed(() => currentLanguage.value),
    loading: computed(() => loading.value),
    error: computed(() => error.value),

    // 方法
    loadLanguagePack,
    setLanguage,
    t,
    getLanguage,
    getAvailableLanguages,
    init
  }
}
```

### 1.2 在组件中使用

```vue
<!-- src/App.vue -->
<template>
  <div id="app">
    <!-- 语言切换器 -->
    <LanguageSwitcher />

    <!-- 主要内容 -->
    <router-view />
  </div>
</template>

<script setup>
import { onMounted } from 'vue'
import { useI18n } from '@/services/i18n'
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'

const { init } = useI18n()

onMounted(() => {
  init()
})
</script>
```

```vue
<!-- src/components/UserManagement.vue -->
<template>
  <div>
    <h1>{{ t('用户管理', 'System') }}</h1>

    <table>
      <thead>
        <tr>
          <th>{{ t('用户名', 'System') }}</th>
          <th>{{ t('邮箱', 'System') }}</th>
          <th>{{ t('操作', 'Common') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="user in users" :key="user.id">
          <td>{{ user.name }}</td>
          <td>{{ user.email }}</td>
          <td>
            <button>{{ t('编辑', 'Common') }}</button>
            <button>{{ t('删除', 'Common') }}</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useI18n } from '@/services/i18n'

const { t } = useI18n()
const users = ref([])

// 加载用户数据
fetchUsers()
</script>
```

### 1.3 语言切换器组件

```vue
<!-- src/components/LanguageSwitcher.vue -->
<template>
  <div class="language-switcher">
    <el-dropdown @command="handleLanguageChange">
      <span class="language-selector">
        {{ currentLang.icon }} {{ currentLang.name }}
        <el-icon><arrow-down /></el-icon>
      </span>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item
            v-for="lang in languages"
            :key="lang.code"
            :command="lang.code"
            :class="{ active: lang.code === currentLanguage }"
          >
            {{ lang.icon }} {{ lang.name }}
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useI18n } from '@/services/i18n'

const { currentLanguage, setLanguage, getAvailableLanguages } = useI18n()

const languages = getAvailableLanguages()
const currentLang = computed(() =>
  languages.find(l => l.code === currentLanguage.value) || languages[0]
)

const handleLanguageChange = async (langCode) => {
  if (langCode !== currentLanguage.value) {
    await setLanguage(langCode)
    // 可以重新加载页面或刷新路由
    window.location.reload()
  }
}
</script>

<style scoped>
.language-switcher {
  display: inline-block;
}

.language-selector {
  cursor: pointer;
  padding: 8px 16px;
  border-radius: 4px;
  transition: background-color 0.3s;
}

.language-selector:hover {
  background-color: #f5f5f5;
}

.active {
  background-color: #e6f7ff;
  color: #1890ff;
}
</style>
```

---

## 📦 实现方案 2：React + Context API

### 2.1 创建 I18n Context

```javascript
// src/contexts/I18nContext.jsx
import React, { createContext, useContext, useState, useEffect } from 'react'
import axios from 'axios'

const I18nContext = createContext()

const API_BASE = 'http://localhost:51479/api/v1'

export function I18nProvider({ children }) {
  const [currentLanguage, setCurrentLanguage] = useState(() =>
    localStorage.getItem('current_language') || 'zh'
  )
  const [languagePacks, setLanguagePacks] = useState({})
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  // 加载语言包
  const loadLanguagePack = async (lang, module = null) => {
    const cacheKey = module
      ? `language_pack_${lang}_${module}`
      : `language_pack_${lang}`

    const cachedData = localStorage.getItem(cacheKey)

    // 检查缓存
    if (cachedData) {
      try {
        const { version, data, timestamp } = JSON.parse(cachedData)
        const isExpired = !timestamp || (Date.now() - timestamp) > 24 * 60 * 60 * 1000

        if (!isExpired) {
          setLanguagePacks(prev => ({ ...prev, [lang]: data }))
          return data
        }
      } catch (e) {
        console.warn('解析缓存失败:', e)
      }
    }

    // 从后端加载
    setLoading(true)
    setError(null)

    try {
      const response = await axios.get(`${API_BASE}/Sys_Language/LanguagePack`, {
        params: { lang, module }
      })

      if (response.data.status) {
        const pack = response.data.data
        setLanguagePacks(prev => ({ ...prev, [lang]: pack }))

        // 缓存
        localStorage.setItem(cacheKey, JSON.stringify({
          version: pack.version,
          data: pack,
          timestamp: Date.now()
        }))

        return pack
      }
    } catch (err) {
      setError(err.message)
      console.error('加载语言包失败:', err)
      throw err
    } finally {
      setLoading(false)
    }
  }

  // 切换语言
  const setLanguage = async (lang) => {
    if (!languagePacks[lang]) {
      await loadLanguagePack(lang)
    }
    setCurrentLanguage(lang)
    localStorage.setItem('current_language', lang)
    document.documentElement.lang = lang
  }

  // 翻译函数
  const t = (key, module = null, defaultValue = key) => {
    const pack = languagePacks[currentLanguage]

    if (!pack) {
      console.warn(`语言包 ${currentLanguage} 未加载`)
      return defaultValue
    }

    // 优先从模块中查找
    if (module && pack.modules && pack.modules[module]) {
      const moduleValue = pack.modules[module][key]
      if (moduleValue) return moduleValue
    }

    // 从扁平结构中查找
    if (pack.flat && pack.flat[key]) {
      return pack.flat[key]
    }

    return defaultValue
  }

  const getAvailableLanguages = () => [
    { code: 'zh', name: '中文', icon: '🇨🇳' },
    { code: 'en', name: 'English', icon: '🇺🇸' },
    { code: 'de', name: 'Deutsch', icon: '🇩🇪' },
    { code: 'id', name: 'Indonesian', icon: '🇮🇩' }
  ]

  // 初始化
  useEffect(() => {
    loadLanguagePack(currentLanguage)
  }, [])

  return (
    <I18nContext.Provider
      value={{
        currentLanguage,
        languagePacks,
        loading,
        error,
        setLanguage,
        t,
        getAvailableLanguages
      }}
    >
      {children}
    </I18nContext.Provider>
  )
}

export const useI18n = () => {
  const context = useContext(I18nContext)
  if (!context) {
    throw new Error('useI18n must be used within I18nProvider')
  }
  return context
}
```

### 2.2 在应用中使用

```jsx
// src/App.jsx
import React from 'react'
import { I18nProvider } from './contexts/I18nContext'
import LanguageSwitcher from './components/LanguageSwitcher'
import UserManagement from './components/UserManagement'

function App() {
  return (
    <I18nProvider>
      <LanguageSwitcher />
      <UserManagement />
    </I18nProvider>
  )
}

export default App
```

```jsx
// src/components/UserManagement.jsx
import React from 'react'
import { useI18n } from '../contexts/I18nContext'

export default function UserManagement() {
  const { t } = useI18n()

  return (
    <div>
      <h1>{t('用户管理', 'System')}</h1>

      <table>
        <thead>
          <tr>
            <th>{t('用户名', 'System')}</th>
            <th>{t('邮箱', 'System')}</th>
            <th>{t('操作', 'Common')}</th>
          </tr>
        </thead>
        <tbody>
          {/* 用户数据 */}
        </tbody>
      </table>
    </div>
  )
}
```

### 2.3 语言切换器组件

```jsx
// src/components/LanguageSwitcher.jsx
import React from 'react'
import { useI18n } from '../contexts/I18nContext'

export default function LanguageSwitcher() {
  const { currentLanguage, setLanguage, getAvailableLanguages } = useI18n()

  const languages = getAvailableLanguages()
  const currentLang = languages.find(l => l.code === currentLanguage) || languages[0]

  const handleLanguageChange = async (langCode) => {
    if (langCode !== currentLanguage) {
      await setLanguage(langCode)
      window.location.reload()
    }
  }

  return (
    <div className="language-switcher">
      <select
        value={currentLanguage}
        onChange={(e) => handleLanguageChange(e.target.value)}
      >
        {languages.map(lang => (
          <option key={lang.code} value={lang.code}>
            {lang.icon} {lang.name}
          </option>
        ))}
      </select>
    </div>
  )
}
```

---

## 🗄️ 数据库示例

```sql
-- 插入示例数据
INSERT INTO Sys_Language (Chinese, ChineseDesc, English, Deutsch, Indonesian, Module, IsPackageContent, Creator, CreateDate)
VALUES
-- 系统模块
('用户管理', '用户管理模块', 'User Management', 'Benutzerverwaltung', 'Manajemen Pengguna', 'System', 0, 'admin', GETDATE()),
('用户名', '登录用户名', 'Username', 'Benutzername', 'Nama Pengguna', 'System', 0, 'admin', GETDATE()),
('密码', '登录密码', 'Password', 'Passwort', 'Kata Sandi', 'System', 0, 'admin', GETDATE()),
('登录', '登录系统', 'Login', 'Anmelden', 'Masuk', 'System', 0, 'admin', GETDATE()),
('退出', '退出系统', 'Logout', 'Abmelden', 'Keluar', 'System', 0, 'admin', GETDATE()),

-- 通用模块
('保存', '保存数据', 'Save', 'Speichern', 'Simpan', 'Common', 0, 'admin', GETDATE()),
('取消', '取消操作', 'Cancel', 'Abbrechen', 'Batal', 'Common', 0, 'admin', GETDATE()),
('删除', '删除数据', 'Delete', 'Löschen', 'Hapus', 'Common', 0, 'admin', GETDATE()),
('编辑', '编辑数据', 'Edit', 'Bearbeiten', 'Edit', 'Common', 0, 'admin', GETDATE()),
('新增', '新增数据', 'Add', 'Hinzufügen', 'Tambah', 'Common', 0, 'admin', GETDATE()),
('搜索', '搜索功能', 'Search', 'Suchen', 'Cari', 'Common', 0, 'admin', GETDATE()),
('导出', '导出数据', 'Export', 'Exportieren', 'Ekspor', 'Common', 0, 'admin', GETDATE()),
('导入', '导入数据', 'Import', 'Importieren', 'Impor', 'Common', 0, 'admin', GETDATE());
```

---

## 📡 API 响应示例

```json
{
  "status": true,
  "message": "获取语言包成功",
  "data": {
    "lang": "en",
    "version": "20250209123456",
    "flat": {
      "用户管理": "User Management",
      "用户名": "Username",
      "密码": "Password"
    },
    "modules": {
      "System": {
        "用户管理": "User Management",
        "用户名": "Username",
        "密码": "Password"
      },
      "Common": {
        "保存": "Save",
        "取消": "Cancel",
        "删除": "Delete"
      }
    }
  }
}
```

---

## 🎯 最佳实践

### 1. **缓存策略**
- 使用 LocalStorage 缓存语言包
- 设置合理的过期时间（建议 24 小时）
- 使用版本号检测更新

### 2. **性能优化**
- 应用启动时预加载当前语言包
- 按需加载模块语言包
- 使用防抖处理频繁的语言切换

### 3. **用户体验**
- 显示加载状态
- 提供回退语言（默认中文）
- 记住用户语言偏好

### 4. **维护建议**
- 使用统一的命名规范
- 定期清理未使用的翻译
- 建立翻译审核流程

---

## 🔧 扩展功能

### 1. **支持动态更新**
```javascript
// 检测语言包更新
const checkForUpdate = async (lang) => {
  const response = await axios.get(`${API_BASE}/Sys_Language/LanguagePack`, {
    params: { lang }
  })

  const newVersion = response.data.data.version
  const cachedVersion = localStorage.getItem(`language_version_${lang}`)

  if (newVersion !== cachedVersion) {
    // 清除旧缓存
    localStorage.removeItem(`language_pack_${lang}`)
    // 重新加载
    return loadLanguagePack(lang)
  }
}
```

### 2. **支持命名空间**
```javascript
// 使用点号分隔的命名空间
t('System.User.Name') // 从 System 模块查找 User.Name
```

### 3. **支持插值**
```javascript
// 支持变量插值
t('欢迎, {name}!', null, null, { name: 'Admin' }) // "Welcome, Admin!"
```

---

这个方案提供了完整的前端多语言实现，你可以根据项目需求选择 Vue 或 React 版本，或者两者都实现！
