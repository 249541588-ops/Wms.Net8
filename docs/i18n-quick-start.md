# 🚀 前端多语言快速开始指南

## 1️⃣ 准备工作

### 后端数据库准备

```sql
-- 插入测试数据
INSERT INTO Sys_Language (Chinese, English, Deutsch, Indonesian, Module, Creator, CreateDate)
VALUES
('用户管理', 'User Management', 'Benutzerverwaltung', 'Manajemen Pengguna', 'System', 'admin', GETDATE()),
('用户名', 'Username', 'Benutzername', 'Nama Pengguna', 'System', 'admin', GETDATE()),
('登录', 'Login', 'Anmelden', 'Masuk', 'Common', 'admin', GETDATE()),
('保存', 'Save', 'Speichern', 'Simpan', 'Common', 'admin', GETDATE());
```

---

## 2️⃣ 后端 API 使用

### 获取语言包

```bash
# 获取所有中文翻译
GET http://localhost:51479/api/v1/Sys_Language/LanguagePack?lang=zh

# 获取英文翻译（System 模块）
GET http://localhost:51479/api/v1/Sys_Language/LanguagePack?lang=en&module=System

# 获取德文翻译
GET http://localhost:51479/api/v1/Sys_Language/LanguagePack?lang=de
```

### 响应示例

```json
{
  "status": true,
  "message": "获取语言包成功",
  "data": {
    "lang": "en",
    "version": "20250209123456",
    "flat": {
      "用户管理": "User Management",
      "用户名": "Username"
    },
    "modules": {
      "System": {
        "用户管理": "User Management",
        "用户名": "Username"
      },
      "Common": {
        "登录": "Login",
        "保存": "Save"
      }
    }
  }
}
```

---

## 3️⃣ Vue 3 快速集成

### 安装依赖

```bash
npm install axios
```

### 创建 i18n 服务

```javascript
// src/services/i18n.js
import { ref, computed } from 'vue'
import axios from 'axios'

const API_BASE = import.meta.env.VITE_API_BASE || 'https://localhost:51479/api/v1'

const currentLanguage = ref(localStorage.getItem('current_language') || 'zh')
const languagePacks = ref({})
const loading = ref(false)

export function useI18n() {
  const loadLanguagePack = async (lang, module = null) => {
    const cacheKey = module ? `language_pack_${lang}_${module}` : `language_pack_${lang}`
    const cached = localStorage.getItem(cacheKey)

    if (cached) {
      const { timestamp } = JSON.parse(cached)
      if (Date.now() - timestamp < 24 * 60 * 60 * 1000) {
        languagePacks.value[lang] = JSON.parse(cached).data
        return JSON.parse(cached).data
      }
    }

    loading.value = true
    try {
      const { data } = await axios.get(`${API_BASE}/Sys_Language/LanguagePack`, {
        params: { lang, module }
      })

      if (data.status) {
        languagePacks.value[lang] = data.data
        localStorage.setItem(cacheKey, JSON.stringify({
          data: data.data,
          timestamp: Date.now()
        }))
        return data.data
      }
    } finally {
      loading.value = false
    }
  }

  const setLanguage = async (lang) => {
    if (!languagePacks.value[lang]) {
      await loadLanguagePack(lang)
    }
    currentLanguage.value = lang
    localStorage.setItem('current_language', lang)
  }

  const t = (key, module = null) => {
    const pack = languagePacks.value[currentLanguage.value]
    if (!pack) return key

    if (module && pack.modules?.[module]?.[key]) {
      return pack.modules[module][key]
    }
    return pack.flat?.[key] || key
  }

  const init = async () => {
    await loadLanguagePack(currentLanguage.value)
  }

  return {
    currentLanguage: computed(() => currentLanguage.value),
    loading: computed(() => loading.value),
    setLanguage,
    t,
    init
  }
}
```

### 在 main.js 中初始化

```javascript
// src/main.js
import { createApp } from 'vue'
import App from './App.vue'
import { useI18n } from './services/i18n'

const app = createApp(App)

// 初始化 i18n
const { init } = useI18n()
init().then(() => {
  app.mount('#app')
})
```

### 在组件中使用

```vue
<template>
  <div>
    <select v-model="currentLanguage" @change="changeLanguage">
      <option value="zh">🇨🇳 中文</option>
      <option value="en">🇺🇸 English</option>
      <option value="de">🇩🇪 Deutsch</option>
    </select>

    <h1>{{ t('用户管理', 'System') }}</h1>
    <button>{{ t('保存', 'Common') }}</button>
  </div>
</template>

<script setup>
import { useI18n } from '@/services/i18n'

const { currentLanguage, setLanguage, t } = useI18n()

const changeLanguage = () => {
  setLanguage(currentLanguage.value)
}
</script>
```

---

## 4️⃣ React 快速集成

### 创建 Context

```javascript
// src/contexts/I18nContext.jsx
import { createContext, useContext, useState, useEffect } from 'react'
import axios from 'axios'

const I18nContext = createContext()

export function I18nProvider({ children }) {
  const [currentLanguage, setCurrentLanguage] = useState('zh')
  const [languagePacks, setLanguagePacks] = useState({})

  const loadLanguagePack = async (lang) => {
    const cached = localStorage.getItem(`language_pack_${lang}`)
    if (cached) {
      setLanguagePacks(prev => ({ ...prev, [lang]: JSON.parse(cached) }))
      return
    }

    const { data } = await axios.get(`/api/v1/Sys_Language/LanguagePack?lang=${lang}`)
    if (data.status) {
      setLanguagePacks(prev => ({ ...prev, [lang]: data.data }))
      localStorage.setItem(`language_pack_${lang}`, JSON.stringify(data.data))
    }
  }

  const t = (key, module = null) => {
    const pack = languagePacks[currentLanguage]
    if (!pack) return key

    if (module && pack.modules?.[module]?.[key]) {
      return pack.modules[module][key]
    }
    return pack.flat?.[key] || key
  }

  useEffect(() => {
    loadLanguagePack(currentLanguage)
  }, [currentLanguage])

  return (
    <I18nContext.Provider value={{ currentLanguage, setCurrentLanguage, t }}>
      {children}
    </I18nContext.Provider>
  )
}

export const useI18n = () => useContext(I18nContext)
```

### 在组件中使用

```jsx
import { useI18n } from '../contexts/I18nContext'

export default function UserManagement() {
  const { currentLanguage, setCurrentLanguage, t } = useI18n()

  return (
    <div>
      <select value={currentLanguage} onChange={(e) => setCurrentLanguage(e.target.value)}>
        <option value="zh">🇨🇳 中文</option>
        <option value="en">🇺🇸 English</option>
      </select>

      <h1>{t('用户管理', 'System')}</h1>
      <button>{t('保存', 'Common')}</button>
    </div>
  )
}
```

---

## 5️⃣ 常见使用场景

### 场景 1：表格列标题

```vue
<template>
  <el-table :data="users">
    <el-table-column :label="t('用户名', 'System')" prop="name" />
    <el-table-column :label="t('邮箱', 'System')" prop="email" />
    <el-table-column :label="t('操作', 'Common')">
      <template #default>
        <el-button>{{ t('编辑', 'Common') }}</el-button>
      </template>
    </el-table-column>
  </el-table>
</template>
```

### 场景 2：表单验证

```javascript
const rules = {
  username: [
    { required: true, message: t('请输入用户名', 'System'), trigger: 'blur' }
  ]
}
```

### 场景 3：动态消息提示

```javascript
ElMessage.success(t('保存成功', 'Common'))
ElMessageBox.confirm(t('确定要删除吗？', 'Common'), t('提示', 'Common'))
```

### 场景 4：菜单和导航

```vue
<template>
  <el-menu>
    <el-menu-item index="1">{{ t('用户管理', 'System') }}</el-menu-item>
    <el-menu-item index="2">{{ t('角色管理', 'System') }}</el-menu-item>
  </el-menu>
</template>
```

---

## 6️⃣ 测试

### 测试切换语言

```javascript
// 1. 加载语言包
await loadLanguagePack('en')

// 2. 切换语言
await setLanguage('en')

// 3. 测试翻译
console.log(t('用户管理', 'System')) // "User Management"
```

### 测试缓存

```javascript
// 第一次调用 - 从 API 加载
await loadLanguagePack('en')

// 第二次调用 - 从缓存加载（快速）
await loadLanguagePack('en')
```

---

## 7️⃣ 故障排查

### 问题 1：翻译不显示

**原因**：语言包未加载

**解决**：
```javascript
// 确保在使用翻译前已初始化
onMounted(async () => {
  await init()
  console.log(t('用户管理')) // 现在可以正常翻译
})
```

### 问题 2：显示的是键而不是翻译

**原因**：翻译键不存在

**解决**：
```javascript
// 检查数据库中是否存在该翻译
// 使用默认值作为后备
const title = t('用户管理', 'System') || 'User Management'
```

### 问题 3：缓存未更新

**原因**：缓存过期时间过长

**解决**：
```javascript
// 清除缓存
localStorage.removeItem('language_pack_en')
localStorage.removeItem('language_version_en')
```

---

## 📝 最佳实践

1. **使用模块组织翻译**：将不同模块的翻译分开管理
2. **提供默认值**：始终提供后备翻译
3. **延迟加载**：按需加载模块语言包
4. **定期清理**：清理未使用的翻译
5. **版本控制**：使用版本号检测更新

---

## 🔗 相关文档

- [完整实现方案](./frontend-i18n-implementation.md)
- [TypeScript 类型定义](./i18n-types.d.ts)
- [API 文档](../src/Wms.Core.WebApi/Controllers/Sys_LanguageController.cs)
