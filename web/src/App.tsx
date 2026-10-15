import { BrowserRouter, Navigate, Outlet, Route, Routes } from "react-router";
import { AuthProvider } from "./auth/AuthContext";
import { useAuth } from "./auth/useAuth";
import { ConfirmProvider } from "./confirm/ConfirmProvider";
import { Layout } from "./components/Layout";
import { PublicLayout } from "./components/PublicLayout";
import { t } from "./i18n/t";
import { AppsPage } from "./pages/AppsPage";
import { ChildPage } from "./pages/ChildPage";
import { ChildrenPage } from "./pages/ChildrenPage";
import { ConfirmEmailChangePage } from "./pages/ConfirmEmailChangePage";
import { ConfirmEmailPage } from "./pages/ConfirmEmailPage";
import { DevicesTab } from "./pages/DevicesTab";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { LoginPage } from "./pages/LoginPage";
import { NewChildPage } from "./pages/NewChildPage";
import { ResetPasswordPage } from "./pages/ResetPasswordPage";
import { RulesTab } from "./pages/RulesTab";
import { SettingsPage } from "./pages/SettingsPage";
import { TodayTab } from "./pages/TodayTab";

function RequireSignIn() {
  const { me } = useAuth();
  if (me === undefined) return <p className="page muted">{t("common.loading")}</p>;
  return me ? <Outlet /> : <Navigate to="/login" replace />;
}

export function App() {
  return (
    <AuthProvider>
      <ConfirmProvider>
        <BrowserRouter>
          <Routes>
            {/* Pages for signed-out parents, including the ones linked from emails. */}
            <Route element={<PublicLayout />}>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/confirm-email" element={<ConfirmEmailPage />} />
              <Route path="/confirm-email-change" element={<ConfirmEmailChangePage />} />
              <Route path="/forgot-password" element={<ForgotPasswordPage />} />
              <Route path="/reset-password" element={<ResetPasswordPage />} />
            </Route>
            <Route element={<RequireSignIn />}>
              <Route element={<Layout />}>
                <Route index element={<ChildrenPage />} />
                <Route path="children/new" element={<NewChildPage />} />
                <Route path="children/:id" element={<ChildPage />}>
                  <Route index element={<Navigate to="today" replace />} />
                  <Route path="today" element={<TodayTab />} />
                  <Route path="rules" element={<RulesTab />} />
                  <Route path="devices" element={<DevicesTab />} />
                </Route>
                <Route path="apps" element={<AppsPage />} />
                <Route path="settings" element={<SettingsPage />} />
                <Route path="*" element={<Navigate to="/" replace />} />
              </Route>
            </Route>
          </Routes>
        </BrowserRouter>
      </ConfirmProvider>
    </AuthProvider>
  );
}
