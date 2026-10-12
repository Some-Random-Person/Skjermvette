import { BrowserRouter, Navigate, Outlet, Route, Routes } from "react-router";
import { AuthProvider } from "./auth/AuthContext";
import { useAuth } from "./auth/useAuth";
import { Layout } from "./components/Layout";
import { t } from "./i18n/t";
import { AppsPage } from "./pages/AppsPage";
import { ChildPage } from "./pages/ChildPage";
import { ChildrenPage } from "./pages/ChildrenPage";
import { DevicesTab } from "./pages/DevicesTab";
import { LoginPage } from "./pages/LoginPage";
import { NewChildPage } from "./pages/NewChildPage";
import { RulesTab } from "./pages/RulesTab";
import { TodayTab } from "./pages/TodayTab";

function RequireSignIn() {
  const { me } = useAuth();
  if (me === undefined) return <p className="page muted">{t("common.loading")}</p>;
  return me ? <Outlet /> : <Navigate to="/login" replace />;
}

export function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
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
              <Route path="*" element={<Navigate to="/" replace />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
