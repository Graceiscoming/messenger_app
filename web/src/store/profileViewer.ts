import { createContext, useContext } from 'react'
import type { UserDto } from '../types/api'

export const ProfileViewerContext = createContext<(user: UserDto) => void>(() => {})

/** Returns a function that opens a user's profile card (your own opens the profile editor). */
export const useOpenProfile = () => useContext(ProfileViewerContext)
