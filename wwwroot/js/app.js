/**
 * app.js — клієнтська логіка ФОП Фінанси
 *
 * Взаємодія з C# відбувається через window.bridge (WebView2 HostObject).
 * Усі виклики bridge є синхронними рядковими методами; відповідь — JSON-рядок.
 *
 * Структура файлу:
 *   1. Bridge / утиліти
 *   2. Стан UI (локальне збереження, вибрана вкладка)
 *   3. Навігація
 *   4. Дашборд
 *   5. Доходи
 *   6. Витрати
 *   7. Категорії
 *   8. Звіти
 *   9. Профіль ФОП
 *  10. Збереження / завантаження файлів
 *  11. Ініціалізація
 */

"use strict";

// ─────────────────────────────────────────
// 1. BRIDGE ТА УТИЛІТИ
// ─────────────────────────────────────────

const PAGE_TITLES = {
  dashboard:  "Огляд",
  incomes:    "Доходи",
  expenses:   "Витрати",
  categories: "Категорії",
  reports:    "Звіти",
  profile:    "Профіль ФОП",
};

function resolveBridge() {
  if (window.bridge) return window.bridge;
  const b = window.chrome?.webview?.hostObjects?.sync?.bridge
         ?? window.chrome?.webview?.hostObjects?.bridge
         ?? null;
  if (b) window.bridge = b;
  return b;
}

function logClient(message) {
  try {
    const b = resolveBridge();
    if (b && typeof b.LogClient === "function")
      b.LogClient(`[${new Date().toISOString()}] ${message}`);
  } catch { /* silent */ }
}

/**
 * Викликає метод C#-моста. Якщо bridge відсутній — повертає { ok: false }.
 */
function callBridge(method, ...args) {
  const b = resolveBridge();
  if (!b) {
    console.warn("Bridge недоступний.");
    return { ok: false, error: "Bridge не підключений" };
  }
  try {
    const raw = b[method](...args);
    if (typeof raw === "string") return JSON.parse(raw);
    return raw;
  } catch (e) {
    logClient(`callBridge failed: ${method}; ${e}`);
    return { ok: false, error: String(e) };
  }
}

// ── Toast ──────────────────────────────────

function showToast(msg, type = "ok") {
  const t = document.getElementById("toast");
  t.textContent = (type === "ok" ? "✓ " : "⚠ ") + msg;
  t.style.borderLeftColor = type === "ok" ? "var(--positive)" : "var(--negative)";
  t.style.borderLeftWidth = "3px";
  t.style.borderLeftStyle = "solid";
  t.classList.add("show");
  clearTimeout(window._toastTimer);
  window._toastTimer = setTimeout(() => t.classList.remove("show"), 3000);
}

// ── Форматування ───────────────────────────

function fmt(amount) {
  return Number(amount).toLocaleString("uk-UA", {
    minimumFractionDigits: 2, maximumFractionDigits: 2
  }) + "\u00a0₴";
}

function fmtDate(isoDate) {
  if (!isoDate) return "—";
  const [y, m, d] = isoDate.split("T")[0].split("-");
  return `${d}.${m}.${y}`;
}

function today() {
  return new Date().toISOString().split("T")[0];
}

function inDateRange(dateValue, start, end) {
  const t = new Date((dateValue || "").split("T")[0]);
  if (Number.isNaN(t.getTime())) return false;
  if (start && t < new Date(start)) return false;
  if (end   && t > new Date(end))   return false;
  return true;
}

function getPeriodValues(startId, endId) {
  return {
    start: document.getElementById(startId)?.value || "",
    end:   document.getElementById(endId)?.value   || "",
  };
}

function setFormError(id, msg) {
  const el = document.getElementById(id);
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("show", !!msg);
}

function escHtml(str) {
  if (!str) return "";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

// ─────────────────────────────────────────
// 2. ЛОКАЛЬНЕ ЗБЕРЕЖЕННЯ НАЛАШТУВАНЬ UI
// ─────────────────────────────────────────

const UI_KEY = "fopfinance_ui";

function loadUiState() {
  try {
    const raw = localStorage.getItem(UI_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch { return {}; }
}

function saveUiState(patch) {
  try {
    const current = loadUiState();
    localStorage.setItem(UI_KEY, JSON.stringify({ ...current, ...patch }));
  } catch { /* storage unavailable */ }
}

// ─────────────────────────────────────────
// 3. НАВІГАЦІЯ МІЖ ВКЛАДКАМИ
// ─────────────────────────────────────────

const TABS = ["dashboard", "incomes", "expenses", "categories", "reports", "profile"];

function openTab(name) {
  TABS.forEach(p => {
    document.getElementById(`page-${p}`)?.classList.toggle("hidden", p !== name);
    document.getElementById(`tab-${p}`)?.classList.toggle("active", p === name);
  });

  const titleEl = document.getElementById("pageTitleLabel");
  if (titleEl) titleEl.textContent = PAGE_TITLES[name] || "";

  saveUiState({ lastTab: name });

  if (name === "dashboard")  renderDashboard();
  if (name === "incomes")    renderIncomes();
  if (name === "expenses")   renderExpenses();
  if (name === "categories") renderCategories();
  if (name === "reports")    {/* no auto-generate */}
  if (name === "profile")    loadProfile();
}

function closeModal(id) {
  document.getElementById(id)?.classList.add("hidden");
}

// Закриття модалок по кліку поза ними
document.addEventListener("click", (e) => {
  if (e.target.classList.contains("modal-backdrop")) {
    e.target.classList.add("hidden");
  }
});

// Закриття по Escape
document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") {
    document.querySelectorAll(".modal-backdrop:not(.hidden)").forEach(m => m.classList.add("hidden"));
  }
});

function openConfirmDialog(message, onConfirm) {
  document.getElementById("confirmMessage").textContent = message;
  window._confirmAction = onConfirm;
  document.getElementById("confirmModal").classList.remove("hidden");
}

function confirmDialogAccept() {
  const cb = window._confirmAction;
  window._confirmAction = null;
  closeModal("confirmModal");
  if (typeof cb === "function") cb();
}

function confirmDialogReject() {
  window._confirmAction = null;
  closeModal("confirmModal");
}

// ─────────────────────────────────────────
// ПРОФІЛІ (список / перемикач)
// ─────────────────────────────────────────

function loadProfiles() {
  const b = resolveBridge();
  let profiles = [];
  try { profiles = JSON.parse(b ? b.GetProfiles() : "[]") || []; } catch { profiles = []; }

  const sel = document.getElementById("profileSelect");
  sel.innerHTML = profiles.map(p => `<option value="${p.id}">${escHtml(p.name)}</option>`).join("");

  // Відновити збережений профіль, або активний з backend
  const ui = loadUiState();
  const savedId = ui.activeProfileId;
  const activeRes = callBridge("GetActiveProfileId");
  const backendId = (activeRes.ok && activeRes.data) ? activeRes.data : null;
  const targetId = savedId || backendId;

  if (targetId && profiles.some(p => p.id === targetId)) {
    sel.value = targetId;
    if (targetId !== backendId) {
      // Синхронізуємо backend якщо відрізняється
      callBridge("SwitchProfile", targetId);
    }
  }
}

function switchProfile(profileId) {
  if (!profileId) return;
  const res = callBridge("SwitchProfile", profileId);
  if (!res.ok) { showToast(res.error, "err"); return; }

  saveUiState({ activeProfileId: profileId });
  loadProfile();
  renderDashboard();
  renderIncomes();
  renderExpenses();
  renderCategories();
  showToast("Профіль переключено");
}

function openProfileModal() {
  // Очищаємо форму
  ["profile-name","profile-fullName","profile-regNumber","profile-kved","profile-iban"].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.value = "";
  });
  const tg = document.getElementById("profile-taxGroup");
  if (tg) tg.value = "3";
  setFormError("profile-error", "");
  document.getElementById("profileModal").classList.remove("hidden");
  setTimeout(() => document.getElementById("profile-name")?.focus(), 50);
}

function submitProfile() {
  const name = document.getElementById("profile-name").value.trim();
  if (!name) {
    setFormError("profile-error", "Назва профілю є обов'язковою.");
    return;
  }

  // Створюємо профіль через bridge
  const res = callBridge("AddProfile", name);
  if (!res.ok) { setFormError("profile-error", res.error); return; }

  const newId = res.data;
  closeModal("profileModal");
  loadProfiles();
  document.getElementById("profileSelect").value = newId;
  switchProfile(newId);

  // Якщо заповнили детальні поля — зберігаємо підприємця
  const fullName = document.getElementById("profile-fullName").value.trim();
  if (fullName) {
    const entrepreneur = {
      fullName,
      taxGroup:           parseInt(document.getElementById("profile-taxGroup").value) || 3,
      registrationNumber: document.getElementById("profile-regNumber").value.trim(),
    };
    callBridge("SaveEntrepreneur", JSON.stringify(entrepreneur));
    updateEntrepreneurLabel(entrepreneur);
  }
}

// ─────────────────────────────────────────
// 4. ДАШБОРД
// ─────────────────────────────────────────

function renderDashboard() {
  const incomes  = getIncomes();
  const expenses = getExpenses();

  const totalIncome  = incomes .reduce((s, i) => s + i.amount, 0);
  const totalExpense = expenses.reduce((s, e) => s + e.amount, 0);
  const netProfit    = totalIncome - totalExpense;

  document.getElementById("stat-income") .textContent = fmt(totalIncome);
  document.getElementById("stat-expense").textContent = fmt(totalExpense);

  const profEl = document.getElementById("stat-profit");
  profEl.textContent = fmt(netProfit);
  profEl.className = "kpi-value " + (netProfit >= 0 ? "pos" : "neg");

  // Cashflow bar
  const flowTotal = totalIncome + totalExpense;
  const incomePct = flowTotal > 0 ? Math.round((totalIncome / flowTotal) * 100) : 50;
  document.getElementById("cashflowIncomeBar").style.width = `${incomePct}%`;
  document.getElementById("cashflowIncomePct") .textContent = `Доходи: ${incomePct}%`;
  document.getElementById("cashflowExpensePct").textContent = `Витрати: ${100 - incomePct}%`;

  // Останні 10 операцій
  const all = [
    ...incomes .map(i => ({ ...i, kind: "income"  })),
    ...expenses.map(e => ({ ...e, kind: "expense" })),
  ].sort((a, b) => new Date(b.date) - new Date(a.date)).slice(0, 10);

  const listEl = document.getElementById("recentList");
  if (!all.length) {
    listEl.innerHTML = '<div class="empty-state"><div class="empty-icon">📭</div>Немає записів. Додайте перший дохід або витрату.</div>';
    return;
  }

  listEl.innerHTML = `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Дата</th>
            <th>Тип</th>
            <th>Опис / Джерело</th>
            <th class="text-right">Сума</th>
          </tr>
        </thead>
        <tbody>
          ${all.map(r => {
            const isIncome = r.kind === "income";
            const label = isIncome ? escHtml(r.source) : escHtml(r.categoryName || "—");
            const detail = r.description ? ` · <span style="color:var(--muted);">${escHtml(r.description)}</span>` : "";
            return `
              <tr>
                <td style="color:var(--muted);font-size:0.78rem;">${fmtDate(r.date)}</td>
                <td>${isIncome
                  ? '<span class="badge badge-income">Дохід</span>'
                  : '<span class="badge badge-expense">Витрата</span>'}</td>
                <td style="font-size:0.8rem;">${label}${detail}</td>
                <td class="text-right text-mono" style="color:${isIncome ? "var(--positive)" : "var(--negative)"};">
                  ${isIncome ? "+" : "–"}${fmt(r.amount)}
                </td>
              </tr>`;
          }).join("")}
        </tbody>
      </table>
    </div>`;
}

// ─────────────────────────────────────────
// 5. ДОХОДИ
// ─────────────────────────────────────────

function getIncomes() {
  try {
    const b = resolveBridge();
    return JSON.parse(b ? b.GetIncomes() : "[]") || [];
  } catch { return []; }
}

function renderIncomes() {
  const list = getIncomes();
  const { start, end } = getPeriodValues("incomeFilterStart", "incomeFilterEnd");
  const filtered = list.filter(i => inDateRange(i.date, start, end));

  const total = filtered.reduce((s, i) => s + i.amount, 0);
  document.getElementById("incomeTotalValue").textContent = filtered.length ? fmt(total) : "—";
  document.getElementById("incomeCountValue").textContent = String(filtered.length);

  const tbody = document.getElementById("incomesBody");
  if (!filtered.length) {
    tbody.innerHTML = '<tr><td colspan="5"><div class="empty-state"><div class="empty-icon">📭</div>Немає доходів</div></td></tr>';
    return;
  }
  tbody.innerHTML = filtered
    .sort((a, b) => new Date(b.date) - new Date(a.date))
    .map(i => `
      <tr>
        <td style="color:var(--muted);font-size:0.78rem;">${fmtDate(i.date)}</td>
        <td style="font-size:0.8rem;">${escHtml(i.source)}</td>
        <td style="color:var(--muted);font-size:0.78rem;">${escHtml(i.description || "")}</td>
        <td class="text-right text-mono" style="color:var(--positive);">+${fmt(i.amount)}</td>
        <td style="text-align:right;white-space:nowrap;">
          <button class="btn-icon" title="Редагувати" onclick='openIncomeModal(${JSON.stringify(JSON.stringify(i))})'>✏️</button>
          <button class="btn-icon danger" title="Видалити" onclick="deleteRecord('${i.id}','incomes')">🗑</button>
        </td>
      </tr>`).join("");
}

function applyIncomeFilter() {
  const { start, end } = getPeriodValues("incomeFilterStart", "incomeFilterEnd");
  if (start && end && start > end) { showToast("Дата початку більша за кінцеву", "err"); return; }
  renderIncomes();
}

function resetIncomeFilter() {
  document.getElementById("incomeFilterStart").value = "";
  document.getElementById("incomeFilterEnd")  .value = "";
  renderIncomes();
}

function openIncomeModal(jsonStr) {
  setFormError("income-error", "");
  const modal = document.getElementById("incomeModal");
  modal.classList.remove("hidden");

  if (jsonStr) {
    const i = JSON.parse(jsonStr);
    document.getElementById("incomeModalTitle").textContent = "Редагувати дохід";
    document.getElementById("income-id")    .value = i.id;
    document.getElementById("income-date")  .value = (i.date || "").split("T")[0];
    document.getElementById("income-source").value = i.source;
    document.getElementById("income-amount").value = i.amount;
    document.getElementById("income-desc")  .value = i.description || "";
  } else {
    document.getElementById("incomeModalTitle").textContent = "Новий дохід";
    document.getElementById("income-id")    .value = "";
    document.getElementById("income-date")  .value = today();
    document.getElementById("income-source").value = "";
    document.getElementById("income-amount").value = "";
    document.getElementById("income-desc")  .value = "";
  }
  setTimeout(() => document.getElementById("income-source").focus(), 50);
}

function submitIncome() {
  const id     = document.getElementById("income-id")    .value.trim();
  const date   = document.getElementById("income-date")  .value;
  const source = document.getElementById("income-source").value.trim();
  const amount = parseFloat(document.getElementById("income-amount").value);
  const desc   = document.getElementById("income-desc")  .value.trim();

  if (!date || !source || isNaN(amount) || amount <= 0) {
    setFormError("income-error", "Заповніть усі обов'язкові поля. Сума має бути > 0.");
    return;
  }

  const payload = JSON.stringify({ id: id || undefined, date, source, amount, description: desc });
  const res = callBridge(id ? "UpdateIncome" : "AddIncome", payload);

  if (res.ok) {
    closeModal("incomeModal");
    renderIncomes();
    renderDashboard();
    showToast(id ? "Дохід оновлено" : "Дохід додано");
  } else {
    setFormError("income-error", res.error);
  }
}

// ─────────────────────────────────────────
// 6. ВИТРАТИ
// ─────────────────────────────────────────

function getExpenses() {
  try {
    const b = resolveBridge();
    return JSON.parse(b ? b.GetExpenses() : "[]") || [];
  } catch { return []; }
}

function populateExpenseFilterCategories() {
  const sel = document.getElementById("expenseFilterCategory");
  if (!sel) return;
  const prev = sel.value;
  const cats = getCategories();
  sel.innerHTML = '<option value="">Всі категорії</option>' +
    cats.map(c => `<option value="${c.id}">${escHtml(c.name)}</option>`).join("");
  if (prev && cats.some(c => c.id === prev)) sel.value = prev;
}

function renderExpenses() {
  populateExpenseFilterCategories();
  const list = getExpenses();
  const { start, end } = getPeriodValues("expenseFilterStart", "expenseFilterEnd");
  const catId = document.getElementById("expenseFilterCategory")?.value || "";

  const filtered = list.filter(e =>
    inDateRange(e.date, start, end) && (!catId || e.categoryId === catId)
  );

  const total = filtered.reduce((s, e) => s + e.amount, 0);
  document.getElementById("expenseTotalValue").textContent = filtered.length ? fmt(total) : "—";
  document.getElementById("expenseCountValue").textContent = String(filtered.length);

  const tbody = document.getElementById("expensesBody");
  if (!filtered.length) {
    tbody.innerHTML = '<tr><td colspan="5"><div class="empty-state"><div class="empty-icon">📭</div>Немає витрат</div></td></tr>';
    return;
  }
  tbody.innerHTML = filtered
    .sort((a, b) => new Date(b.date) - new Date(a.date))
    .map(e => `
      <tr>
        <td style="color:var(--muted);font-size:0.78rem;">${fmtDate(e.date)}</td>
        <td><span class="badge badge-expense">${escHtml(e.categoryName || "—")}</span></td>
        <td style="color:var(--muted);font-size:0.78rem;">${escHtml(e.description || "")}</td>
        <td class="text-right text-mono" style="color:var(--negative);">–${fmt(e.amount)}</td>
        <td style="text-align:right;white-space:nowrap;">
          <button class="btn-icon" title="Редагувати" onclick='openExpenseModal(${JSON.stringify(JSON.stringify(e))})'>✏️</button>
          <button class="btn-icon danger" title="Видалити" onclick="deleteRecord('${e.id}','expenses')">🗑</button>
        </td>
      </tr>`).join("");
}

function applyExpenseFilter() {
  const { start, end } = getPeriodValues("expenseFilterStart", "expenseFilterEnd");
  if (start && end && start > end) { showToast("Дата початку більша за кінцеву", "err"); return; }
  renderExpenses();
}

function resetExpenseFilter() {
  document.getElementById("expenseFilterStart").value = "";
  document.getElementById("expenseFilterEnd")  .value = "";
  const catSel = document.getElementById("expenseFilterCategory");
  if (catSel) catSel.value = "";
  renderExpenses();
}

function openExpenseModal(jsonStr) {
  const cats = getCategories();
  const sel  = document.getElementById("expense-category");
  sel.innerHTML = cats.length
    ? cats.map(c => `<option value="${c.id}">${escHtml(c.name)}</option>`).join("")
    : '<option value="">— Немає категорій —</option>';

  setFormError("expense-error", "");
  document.getElementById("expenseModal").classList.remove("hidden");

  if (jsonStr) {
    const e = JSON.parse(jsonStr);
    document.getElementById("expenseModalTitle").textContent = "Редагувати витрату";
    document.getElementById("expense-id")    .value = e.id;
    document.getElementById("expense-date")  .value = (e.date || "").split("T")[0];
    sel.value = e.categoryId;
    document.getElementById("expense-amount").value = e.amount;
    document.getElementById("expense-desc")  .value = e.description || "";
  } else {
    document.getElementById("expenseModalTitle").textContent = "Нова витрата";
    document.getElementById("expense-id")    .value = "";
    document.getElementById("expense-date")  .value = today();
    document.getElementById("expense-amount").value = "";
    document.getElementById("expense-desc")  .value = "";
  }
  setTimeout(() => document.getElementById("expense-amount").focus(), 50);
}

function submitExpense() {
  const id     = document.getElementById("expense-id")    .value.trim();
  const date   = document.getElementById("expense-date")  .value;
  const catSel = document.getElementById("expense-category");
  const catId  = catSel.value;
  const catName = catSel.options[catSel.selectedIndex]?.text || "";
  const amount = parseFloat(document.getElementById("expense-amount").value);
  const desc   = document.getElementById("expense-desc").value.trim();

  if (!date || !catId || isNaN(amount) || amount <= 0) {
    setFormError("expense-error", "Заповніть усі обов'язкові поля. Сума має бути > 0.");
    return;
  }

  const payload = JSON.stringify({ id: id || undefined, date, categoryId: catId, categoryName: catName, amount, description: desc });
  const res = callBridge(id ? "UpdateExpense" : "AddExpense", payload);

  if (res.ok) {
    closeModal("expenseModal");
    renderExpenses();
    renderDashboard();
    showToast(id ? "Витрату оновлено" : "Витрату додано");
  } else {
    setFormError("expense-error", res.error);
  }
}

// ─────────────────────────────────────────
// ВИДАЛЕННЯ
// ─────────────────────────────────────────

function deleteRecord(id, context) {
  openConfirmDialog("Видалити цей запис? Дію не можна скасувати.", () => {
    const res = callBridge("RemoveRecord", id);
    if (res.ok) {
      if (context === "incomes")  renderIncomes();
      if (context === "expenses") renderExpenses();
      renderDashboard();
      showToast("Запис видалено");
    } else {
      showToast(res.error, "err");
    }
  });
}

// ─────────────────────────────────────────
// 7. КАТЕГОРІЇ
// ─────────────────────────────────────────

function getCategories() {
  try {
    const b = resolveBridge();
    return JSON.parse(b ? b.GetCategories() : "[]") || [];
  } catch { return []; }
}

function renderCategories() {
  const cats = getCategories();
  const expenses = getExpenses();

  document.getElementById("categoryCountValue").textContent = String(cats.length);

  // Порахувати категорії зі списаннями
  const usedIds = new Set(expenses.map(e => e.categoryId));
  const usedCount = cats.filter(c => usedIds.has(c.id)).length;
  const usedEl = document.getElementById("categoriesUsedValue");
  if (usedEl) usedEl.textContent = String(usedCount);

  const grid = document.getElementById("categoriesGrid");
  if (!cats.length) {
    grid.innerHTML = '<div class="empty-state" style="grid-column:1/-1;"><div class="empty-icon">🏷️</div>Немає категорій. Додайте першу!</div>';
    return;
  }
  grid.innerHTML = cats.map(c => {
    const expCount = expenses.filter(e => e.categoryId === c.id).length;
    const expTotal = expenses.filter(e => e.categoryId === c.id).reduce((s, e) => s + e.amount, 0);
    return `
      <div class="category-card">
        <div class="category-name">${escHtml(c.name)}</div>
        ${c.description ? `<div class="category-desc">${escHtml(c.description)}</div>` : ""}
        <div style="font-size:0.72rem;color:var(--muted);margin-top:0.25rem;">
          ${expCount} записів · ${expCount ? fmt(expTotal) : "0\u00a0₴"}
        </div>
        <div class="category-actions">
          <button class="btn-ghost flex-1" style="font-size:0.75rem;" onclick='openCategoryModal(${JSON.stringify(JSON.stringify(c))})'>✏️ Редагувати</button>
          <button class="btn-icon danger" onclick="deleteCategory('${c.id}')">🗑</button>
        </div>
      </div>`;
  }).join("");
}

function openCategoryModal(jsonStr) {
  setFormError("cat-error", "");
  document.getElementById("categoryModal").classList.remove("hidden");
  if (jsonStr) {
    const c = JSON.parse(jsonStr);
    document.getElementById("categoryModalTitle").textContent = "Редагувати категорію";
    document.getElementById("cat-id")  .value = c.id;
    document.getElementById("cat-name").value = c.name;
    document.getElementById("cat-desc").value = c.description || "";
  } else {
    document.getElementById("categoryModalTitle").textContent = "Нова категорія";
    document.getElementById("cat-id")  .value = "";
    document.getElementById("cat-name").value = "";
    document.getElementById("cat-desc").value = "";
  }
  setTimeout(() => document.getElementById("cat-name").focus(), 50);
}

function submitCategory() {
  const id   = document.getElementById("cat-id")  .value.trim();
  const name = document.getElementById("cat-name").value.trim();
  const desc = document.getElementById("cat-desc").value.trim();

  if (!name) { setFormError("cat-error", "Назва є обов'язковою."); return; }

  const payload = JSON.stringify({ id: id || undefined, name, description: desc });
  const res = callBridge(id ? "UpdateCategory" : "AddCategory", payload);

  if (res.ok) {
    closeModal("categoryModal");
    renderCategories();
    showToast(id ? "Категорію оновлено" : "Категорію додано");
  } else {
    setFormError("cat-error", res.error);
  }
}

function deleteCategory(id) {
  openConfirmDialog("Видалити категорію? Усі витрати з цією категорією залишаться, але без назви.", () => {
    const res = callBridge("RemoveCategory", id);
    if (res.ok) { renderCategories(); showToast("Категорію видалено"); }
    else         { showToast(res.error, "err"); }
  });
}

// ─────────────────────────────────────────
// 8. ЗВІТИ
// ─────────────────────────────────────────

let _lastReport = null;

function generateReport() {
  const start = document.getElementById("reportStart").value;
  const end   = document.getElementById("reportEnd")  .value;
  if (!start || !end) { showToast("Вкажіть обидві дати", "err"); return; }
  if (start > end)    { showToast("Початок не може бути пізніше кінця", "err"); return; }

  const b = resolveBridge();
  let report;
  try { report = JSON.parse(b ? b.GenerateReport(start, end) : "{}"); }
  catch { showToast("Помилка формування звіту", "err"); return; }

  _lastReport = report;

  document.getElementById("rep-income") .textContent = fmt(report.totalIncome  || 0);
  document.getElementById("rep-expense").textContent = fmt(report.totalExpense || 0);

  const profit = (report.totalIncome || 0) - (report.totalExpense || 0);
  const profEl = document.getElementById("rep-profit");
  profEl.textContent = fmt(profit);
  profEl.className = "kpi-value " + (profit >= 0 ? "pos" : "neg");

  document.getElementById("rep-incomes-body").innerHTML =
    (report.incomes || []).map(i =>
      `<tr>
        <td style="color:var(--muted);font-size:0.78rem;">${fmtDate(i.date)}</td>
        <td style="font-size:0.8rem;">${escHtml(i.source)}</td>
        <td class="text-right text-mono" style="color:var(--positive);">+${fmt(i.amount)}</td>
      </tr>`).join("") || `<tr><td colspan="3"><div class="empty-state">Немає доходів у цьому періоді</div></td></tr>`;

  document.getElementById("rep-expenses-body").innerHTML =
    (report.expenses || []).map(e =>
      `<tr>
        <td style="color:var(--muted);font-size:0.78rem;">${fmtDate(e.date)}</td>
        <td><span class="badge badge-expense">${escHtml(e.categoryName || "—")}</span></td>
        <td class="text-right text-mono" style="color:var(--negative);">–${fmt(e.amount)}</td>
      </tr>`).join("") || `<tr><td colspan="3"><div class="empty-state">Немає витрат у цьому періоді</div></td></tr>`;

  document.getElementById("reportResult").classList.remove("hidden");
  showToast("Звіт сформовано");
}

function exportReport(format) {
  if (!_lastReport) { showToast("Спочатку сформуйте звіт", "err"); return; }
  const res = callBridge("ExportReport", JSON.stringify(_lastReport), format);
  if (res.ok && res.data !== "cancelled") showToast(`Звіт збережено: ${res.data}`);
  else if (res.error) showToast(res.error, "err");
}

function applyReportTemplate(template) {
  const now   = new Date();
  const year  = now.getFullYear();
  const month = now.getMonth();
  let start, end;

  if (template === "week") {
    const day = now.getDay() || 7;
    start = new Date(now);
    start.setDate(now.getDate() - day + 1);
    end = new Date(start);
    end.setDate(start.getDate() + 6);
  } else if (template === "month") {
    start = new Date(year, month, 1);
    end   = new Date(year, month + 1, 0);
  } else if (template === "quarter") {
    const qs = Math.floor(month / 3) * 3;
    start = new Date(year, qs, 1);
    end   = new Date(year, qs + 3, 0);
  } else {
    start = new Date(year, 0, 1);
    end   = new Date(year, 11, 31);
  }

  document.getElementById("reportStart").value = start.toISOString().split("T")[0];
  document.getElementById("reportEnd")  .value = end.toISOString().split("T")[0];
}

// ─────────────────────────────────────────
// 9. ПРОФІЛЬ ФОП
// ─────────────────────────────────────────

function loadProfile() {
  const b = resolveBridge();
  let e;
  try { e = JSON.parse(b ? b.GetEntrepreneur() : "{}") || {}; }
  catch { return; }

  document.getElementById("prof-fullName") .value = e.fullName           || "";
  document.getElementById("prof-taxGroup") .value = String(e.taxGroup    || 3);
  document.getElementById("prof-regNumber").value = e.registrationNumber || "";

  // Розширені поля (зберігаються локально в localStorage, т.к. backend не знає про них)
  const local = loadUiState();
  const extra = local.profileExtra || {};
  document.getElementById("prof-address").value = extra.address || "";
  document.getElementById("prof-phone")  .value = extra.phone   || "";
  document.getElementById("prof-email")  .value = extra.email   || "";
  document.getElementById("prof-kved")   .value = extra.kved    || "";
  document.getElementById("prof-iban")   .value = extra.iban    || "";
  const vatEl = document.getElementById("prof-vat");
  if (vatEl) vatEl.value = String(extra.vat || "false");

  updateEntrepreneurLabel(e);
}

function saveProfile() {
  const fullName = document.getElementById("prof-fullName").value.trim();
  if (!fullName) { showToast("ПІБ є обов'язковим", "err"); return; }

  const e = {
    fullName,
    taxGroup:           parseInt(document.getElementById("prof-taxGroup") .value) || 3,
    registrationNumber: document.getElementById("prof-regNumber").value.trim(),
  };
  const res = callBridge("SaveEntrepreneur", JSON.stringify(e));

  if (!res.ok) { showToast(res.error, "err"); return; }

  // Зберігаємо розширені поля локально
  const extra = {
    address: document.getElementById("prof-address").value.trim(),
    phone:   document.getElementById("prof-phone")  .value.trim(),
    email:   document.getElementById("prof-email")  .value.trim(),
    kved:    document.getElementById("prof-kved")   .value.trim(),
    iban:    document.getElementById("prof-iban")   .value.trim(),
    vat:     document.getElementById("prof-vat")?.value || "false",
  };
  saveUiState({ profileExtra: extra });

  updateEntrepreneurLabel(e);
  showToast("Профіль збережено");
}

function updateEntrepreneurLabel(e) {
  const lbl = document.getElementById("entrepreneurLabel");
  if (!lbl) return;
  if (e && e.fullName)
    lbl.textContent = `${e.fullName} · Група ${e.taxGroup}${e.registrationNumber ? " · " + e.registrationNumber : ""}`;
  else
    lbl.textContent = "";
}

// ─────────────────────────────────────────
// 10. ФАЙЛИ
// ─────────────────────────────────────────

function saveData(format) {
  const res = callBridge("SaveData", format);
  if (res.ok && res.data !== "cancelled") showToast(`Дані збережено (${res.data})`);
  else if (res.error) showToast(res.error, "err");
}

function loadData() {
  const res = callBridge("LoadData");
  if (res.ok && res.data === "loaded") {
    renderDashboard();
    loadProfile();
    renderIncomes();
    renderExpenses();
    renderCategories();
    showToast("Дані завантажено");
  } else if (res.error) {
    showToast(res.error, "err");
  }
}

// ─────────────────────────────────────────
// 11. ІНІЦІАЛІЗАЦІЯ
// ─────────────────────────────────────────

document.addEventListener("DOMContentLoaded", () => {
  window.addEventListener("error", ev =>
    logClient(`window.onerror: ${ev.message} at ${ev.filename}:${ev.lineno}`));
  window.addEventListener("unhandledrejection", ev =>
    logClient(`unhandledrejection: ${ev.reason}`));

  const b = resolveBridge();
  if (!b) {
    showToast("Bridge не підключений. Перевірте backend.", "err");
    return;
  }

  const ping = callBridge("Ping");
  if (!ping.ok) {
    showToast("Bridge відповів з помилкою.", "err");
    return;
  }
  logClient("Bridge handshake OK");

  // Дата для звітів — поточний місяць
  const now = new Date();
  const y   = now.getFullYear();
  const m   = String(now.getMonth() + 1).padStart(2, "0");
  const last = new Date(y, now.getMonth() + 1, 0).getDate();
  document.getElementById("reportStart").value = `${y}-${m}-01`;
  document.getElementById("reportEnd")  .value = `${y}-${m}-${String(last).padStart(2, "0")}`;

  // Завантаження
  loadProfiles();
  populateExpenseFilterCategories();
  loadProfile();
  renderDashboard();

  // Відновити останню вкладку
  const ui = loadUiState();
  if (ui.lastTab && TABS.includes(ui.lastTab)) {
    openTab(ui.lastTab);
  }
});
