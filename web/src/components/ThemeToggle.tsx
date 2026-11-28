import { useT } from '../i18n'
import { resolveTheme, updateSettings, useSettings } from '../store/settings'
import { MoonIcon, SunIcon } from './icons'

/** Quick light/dark switch; picks the opposite of the currently resolved theme. */
export default function ThemeToggle({ className = '' }: { className?: string }) {
  const { t } = useT()
  const s = useSettings()
  const dark = resolveTheme(s) === 'dark'
  return (
    <button
      className={`icon-btn ${className}`}
      aria-label={dark ? t('theme.toLight') : t('theme.toDark')}
      title={dark ? t('theme.toLight') : t('theme.toDark')}
      onClick={() => updateSettings({ theme: dark ? 'light' : 'dark' })}
    >
      {dark ? <SunIcon /> : <MoonIcon />}
    </button>
  )
}
