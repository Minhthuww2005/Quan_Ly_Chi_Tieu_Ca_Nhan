import { Toaster } from './components/ui/sonner';
import { TooltipProvider } from './components/ui/tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useEffect, useState } from 'react';
import HomePage from './pages/HomePage';
import Index from './pages/Index';
import NotFound from './pages/NotFound';
import CardsPage from './pages/CardsPage';
import HistoryPage from './pages/HistoryPage';
import ProfilePage from './pages/ProfilePage';
import AdminLogin from './pages/AdminLogin';
import AdminDashboard from './pages/AdminDashboard';
import AdminUserManagement from './pages/AdminUserManagement';
import AdminTransactionManagement from './pages/AdminTransactionManagement';
import AdminKYCManagement from './pages/AdminKYCManagement';
import CardManagementPage from './pages/CardManagementPage';
import SavingsPage from './pages/SavingsPage';
import InvestmentPage from './pages/InvestmentPage';
import LoanPage from './pages/LoanPage';
import SecurityPage from './pages/SecurityPage';
import SettingsPage from './pages/SettingsPage';
import NotificationsPage from './pages/NotificationsPage';
import InsurancePage from './pages/InsurancePage';
import PaymentsPage from './pages/PaymentsPage';
import PaymentReturnPage from './pages/PaymentReturnPage';
import GroupExpensesPage from './pages/GroupExpensesPage';

const queryClient = new QueryClient();

// Component kiểm tra xác thực
function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const [isLoggedIn, setIsLoggedIn] = useState<boolean | null>(null);

  useEffect(() => {
    if (localStorage.getItem('isLoggedIn') !== 'true' || !localStorage.getItem('token')) { setIsLoggedIn(false); return; }
    fetch('/api/auth/me').then(async response => {
      if (!response.ok) { for (const key of ['token', 'userInfo', 'isLoggedIn']) localStorage.removeItem(key); setIsLoggedIn(false); return; }
      const data = await response.json(); localStorage.setItem('userInfo', JSON.stringify(data.user)); setIsLoggedIn(true);
    }).catch(() => setIsLoggedIn(false));
  }, []);

  if (isLoggedIn === null) {
    // Đang kiểm tra trạng thái đăng nhập
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 border-4 border-gray-300 border-t-gray-900 rounded-full animate-spin mx-auto mb-4"></div>
          <p className="text-gray-600">Đang tải...</p>
        </div>
      </div>
    );
  }

  return isLoggedIn ? <>{children}</> : <Navigate to="/" replace />;
}

// Component kiểm tra admin authentication
function AdminProtectedRoute({ children }: { children: React.ReactNode }) {
  const [isAdmin, setIsAdmin] = useState<boolean | null>(null);

  useEffect(() => {
    const adminInfo = localStorage.getItem('adminInfo');
    setIsAdmin(!!adminInfo);
  }, []);

  if (isAdmin === null) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 border-4 border-gray-300 border-t-gray-900 rounded-full animate-spin mx-auto mb-4"></div>
          <p className="text-gray-600">Đang tải...</p>
        </div>
      </div>
    );
  }

  return isAdmin ? <>{children}</> : <Navigate to="/admin/login" replace />;
}

const App = () => (
  <QueryClientProvider client={queryClient}>
    <TooltipProvider>
      <Toaster />
      <BrowserRouter>
        <Routes>
          {/* Trang chủ - không cần đăng nhập */}
          <Route path="/" element={<HomePage />} />
          <Route path="/payments/return" element={<PaymentReturnPage />} />
          <Route path="/settings" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/notifications" element={<ProtectedRoute><NotificationsPage /></ProtectedRoute>} />
          <Route path="/insurance" element={<ProtectedRoute><InsurancePage /></ProtectedRoute>} />
          <Route path="/payments" element={<ProtectedRoute><PaymentsPage /></ProtectedRoute>} />
          
          {/* Các trang cần đăng nhập */}
          <Route path="/wallet" element={
            <ProtectedRoute>
              <Index />
            </ProtectedRoute>
          } />
          <Route path="/cards" element={
            <ProtectedRoute>
              <CardsPage />
            </ProtectedRoute>
          } />
          <Route path="/card-management" element={
            <ProtectedRoute>
              <CardManagementPage />
            </ProtectedRoute>
          } />
          <Route path="/history" element={
            <ProtectedRoute>
              <HistoryPage />
            </ProtectedRoute>
          } />
          <Route path="/profile" element={
            <ProtectedRoute>
              <ProfilePage />
            </ProtectedRoute>
          } />
          <Route path="/savings" element={
            <ProtectedRoute>
              <SavingsPage />
            </ProtectedRoute>
          } />
          <Route path="/investments" element={
            <ProtectedRoute>
              <InvestmentPage />
            </ProtectedRoute>
          } />
          <Route path="/loans" element={
            <ProtectedRoute>
              <LoanPage />
            </ProtectedRoute>
          } />
          <Route path="/security" element={
            <ProtectedRoute>
              <SecurityPage />
            </ProtectedRoute>
          } />
          
          <Route path="/groups" element={<ProtectedRoute><GroupExpensesPage /></ProtectedRoute>} />
          <Route path="/admin/groups" element={<AdminProtectedRoute><GroupExpensesPage admin /></AdminProtectedRoute>} />
          {/* Admin routes */}
          <Route path="/admin/login" element={<AdminLogin />} />
          <Route path="/admin/dashboard" element={
            <AdminProtectedRoute>
              <AdminDashboard />
            </AdminProtectedRoute>
          } />
          <Route path="/admin/users" element={
            <AdminProtectedRoute>
              <AdminUserManagement />
            </AdminProtectedRoute>
          } />
          <Route path="/admin/transactions" element={
            <AdminProtectedRoute>
              <AdminTransactionManagement />
            </AdminProtectedRoute>
          } />
          <Route path="/admin/kyc" element={
            <AdminProtectedRoute>
              <AdminKYCManagement />
            </AdminProtectedRoute>
          } />
          
          {/* Trang 404 */}
          <Route path="*" element={<NotFound />} />
        </Routes>
      </BrowserRouter>
    </TooltipProvider>
  </QueryClientProvider>
);

export default App;
