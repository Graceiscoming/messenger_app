import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { ApiError } from './api/client'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import ChatRoom from './pages/ChatRoom'
import MainLayout, { EmptyChat } from './pages/MainLayout'
import { AuthProvider } from './store/auth'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (count, err) => !(err instanceof ApiError && err.status >= 400 && err.status < 500) && count < 2,
    },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route element={<MainLayout />}>
              <Route path="/" element={<EmptyChat />} />
              <Route path="/c/:id" element={<ChatRoom />} />
              <Route path="*" element={<EmptyChat />} />
            </Route>
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
