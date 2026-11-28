import { useCallback } from 'react'
import { getSettings, useSettings, type Language } from '../store/settings'
import { en } from './en'
import { th, type Dict } from './th'

export type TKey = keyof Dict
export type Vars = Record<string, string | number>

const dicts: Record<Language, Dict> = { th, en }

/** Non-hook translation for utilities (callers re-render because they also use useT / useSettings). */
export function translate(key: TKey, vars?: Vars, lang: Language = getSettings().language): string {
  let s = dicts[lang][key]
  if (vars) for (const [k, v] of Object.entries(vars)) s = s.replaceAll(`{${k}}`, String(v))
  return s
}

export function useT() {
  const { language } = useSettings()
  const t = useCallback((key: TKey, vars?: Vars) => translate(key, vars, language), [language])
  return { t, lang: language }
}

export const localeOf = (lang: Language) => (lang === 'th' ? 'th-TH' : 'en-US')
