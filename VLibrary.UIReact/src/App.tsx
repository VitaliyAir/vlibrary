import { useAuth } from 'react-oidc-context';
import { NavLink, Outlet, Route, Routes } from 'react-router';
import { useDuplicates } from './api/hooks';
import { ReminderDialog } from './components/ReminderDialog';
import { BookPage, NewBookPage } from './pages/BookPage';
import { AuthorsPage, ClassifiersPage, SettingsPage } from './pages/CatalogAdminPages';
import { CatalogPage } from './pages/CatalogPage';
import { DuplicatesPage } from './pages/DuplicatesPage';
import { ImportPage } from './pages/ImportPage';
import { NotesPage } from './pages/NotesPage';
import { ReadingRoomPage } from './pages/ReadingRoomPage';
import { ShelvesPage } from './pages/ShelvesPage';

function Layout() {
  const auth = useAuth();
  const duplicates = useDuplicates();
  const pending = duplicates.data?.length ?? 0;

  return (
    <div className="layout">
      <nav className="sidebar">
        <div className="brand">VLibrary</div>
        <NavLink to="/" end>
          Catalog
        </NavLink>
        <NavLink to="/reading-room">Reading room</NavLink>
        <NavLink to="/shelves">Shelves</NavLink>
        <NavLink to="/notes">Notes</NavLink>
        <hr />
        <NavLink to="/import">Import</NavLink>
        <NavLink to="/duplicates">
          Duplicates {pending > 0 && <span className="count">{pending}</span>}
        </NavLink>
        <NavLink to="/authors">Authors</NavLink>
        <NavLink to="/classifiers">Classification</NavLink>
        <NavLink to="/settings">Settings</NavLink>
        <div className="user-box">
          <span>{auth.user?.profile.preferred_username}</span>
          <button type="button" onClick={() => void auth.signoutRedirect()}>
            Sign out
          </button>
        </div>
      </nav>
      <main>
        <Outlet />
      </main>
      <ReminderDialog />
    </div>
  );
}

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<CatalogPage />} />
        <Route path="books/new" element={<NewBookPage />} />
        <Route path="books/:id" element={<BookPage />} />
        <Route path="reading-room" element={<ReadingRoomPage />} />
        <Route path="shelves" element={<ShelvesPage />} />
        <Route path="notes" element={<NotesPage />} />
        <Route path="import" element={<ImportPage />} />
        <Route path="duplicates" element={<DuplicatesPage />} />
        <Route path="authors" element={<AuthorsPage />} />
        <Route path="classifiers" element={<ClassifiersPage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="*" element={<p>Page not found.</p>} />
      </Route>
    </Routes>
  );
}
