/**
 * 前端多语言 TypeScript 类型定义
 * 适用于 Vue 3 / React / TypeScript 项目
 */

/**
 * 支持的语言类型
 */
export type SupportedLanguage = 'zh' | 'en' | 'de' | 'id'

/**
 * 语言信息接口
 */
export interface LanguageInfo {
  code: SupportedLanguage
  name: string
  icon: string
}

/**
 * 语言包结构（扁平）
 */
export interface FlatLanguagePack {
  [key: string]: string
}

/**
 * 语言包结构（模块化）
 */
export interface ModuleLanguagePack {
  [moduleName: string]: FlatLanguagePack
}

/**
 * 完整语言包
 */
export interface LanguagePack {
  lang: SupportedLanguage
  version: string
  flat: FlatLanguagePack
  modules: ModuleLanguagePack
}

/**
 * 翻译选项
 */
export interface TranslateOptions {
  /**
   * 模块名称
   */
  module?: string

  /**
   * 默认值（当翻译不存在时使用）
   */
  defaultValue?: string

  /**
   * 插值参数
   */
  args?: Record<string, string | number>

  /**
   * 是否使用命名空间（例如：System.User.Name）
   */
  namespace?: boolean
}

/**
 * I18n 服务接口
 */
export interface I18nService {
  /**
   * 当前语言
   */
  currentLanguage: SupportedLanguage

  /**
   * 语言包数据
   */
  languagePacks: Record<SupportedLanguage, LanguagePack>

  /**
   * 加载状态
   */
  loading: boolean

  /**
   * 错误信息
   */
  error: string | null

  /**
   * 加载语言包
   * @param lang - 语言代码
   * @param module - 模块名称（可选）
   */
  loadLanguagePack(lang: SupportedLanguage, module?: string): Promise<LanguagePack>

  /**
   * 切换语言
   * @param lang - 语言代码
   */
  setLanguage(lang: SupportedLanguage): Promise<void>

  /**
   * 翻译函数
   * @param key - 翻译键
   * @param options - 翻译选项
   */
  t(key: string, options?: TranslateOptions): string

  /**
   * 获取当前语言
   */
  getLanguage(): SupportedLanguage

  /**
   * 获取可用语言列表
   */
  getAvailableLanguages(): LanguageInfo[]

  /**
   * 初始化 i18n
   */
  init(): Promise<void>
}

/**
 * 后端 API 响应类型
 */
export interface LanguagePackApiResponse {
  status: boolean
  message: string
  data: LanguagePack
}

/**
 * 缓存数据结构
 */
export interface CachedLanguagePack {
  version: string
  data: LanguagePack
  timestamp: number
}

/**
 * 命名空间翻译键类型
 * 例如：'System.User.Name' -> { System: { User: { Name: string } } }
 */
export type NamespaceKey = string

/**
 * 翻译键类型（可以是简单键或命名空间键）
 */
export type TranslateKey = string | NamespaceKey

/**
 * I18n 配置选项
 */
export interface I18nConfig {
  /**
   * 默认语言
   */
  defaultLanguage?: SupportedLanguage

  /**
   * API 基础路径
   */
  apiBaseUrl?: string

  /**
   * 缓存过期时间（毫秒）
   * 默认：24小时
   */
  cacheExpireTime?: number

  /**
   * 是否启用缓存
   */
  enableCache?: boolean

  /**
   * 回退语言（当翻译不存在时使用）
   */
  fallbackLanguage?: SupportedLanguage

  /**
   * 缺失翻译时的处理方式
   * - 'returnKey': 返回键本身
   * - 'returnDefault': 返回默认值
   * - 'throw': 抛出错误
   */
  missingTranslation?: 'returnKey' | 'returnDefault' | 'throw'
}

/**
 * Vue 3 Composable 类型
 */
export interface UseI18nReturn {
  currentLanguage: Readonly<Ref<SupportedLanguage>>
  loading: Readonly<Ref<boolean>>
  error: Readonly<Ref<string | null>>
  loadLanguagePack: (lang: SupportedLanguage, module?: string) => Promise<LanguagePack>
  setLanguage: (lang: SupportedLanguage) => Promise<void>
  t: (key: string, options?: TranslateOptions) => string
  getLanguage: () => SupportedLanguage
  getAvailableLanguages: () => LanguageInfo[]
  init: () => Promise<void>
}

/**
 * React Context 类型
 */
export interface I18nContextValue extends I18nService {}

/**
 * 语言更新事件
 */
export interface LanguageUpdateEvent {
  language: SupportedLanguage
  timestamp: number
  version: string
}

/**
 * 语言切换 Hook 类型
 */
export type OnLanguageChangeCallback = (lang: SupportedLanguage) => void | Promise<void>

/**
 * 批量翻译选项
 */
export interface BatchTranslateOptions {
  keys: string[]
  module?: string
  namespace?: boolean
}

/**
 * 批量翻译结果
 */
export interface BatchTranslateResult {
  [key: string]: string
}
