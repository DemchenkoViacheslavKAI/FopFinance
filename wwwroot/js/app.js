/**
 * app.js — клієнтська логіка ФОП Фінанси
 *
 * Взаємодія з C# відбувається через window.bridge (WebView2 HostObject).
 * Усі виклики bridge є синхронними рядковими методами; відповідь — JSON-рядок.
 *
 * Структура файлу:
 *   1. Утиліти (bridge-виклики, toast, форматування)
 *   2. Навігація між вкладками
 *   3. Дашборд
 *   4. Доходи (список, форма, CRUD)
 *   5. Витрати
 *   6. Категорії
 *   7. Звіти
 *   8. Профіль ФОП
 *   9. Збереження / завантаження файлів
 *  10. Ініціалізація
 */

"use strict";

/**
 * Повертає bridge-об'єкт WebView2.
 * Головний шлях: window.chrome.webview.hostObjects.sync.bridge
 */
function resolveBridge() {
  if (window.bridge) return window.bridge;

  const fromHost = window.chrome?.webview?.hostObjects?.sync?.bridge
                ?? window.chrome?.webview?.hostObjects?.bridge
                ?? null;

  if (fromHost) {
    window.bridge = fromHost;
    return fromHost;
  }

  return null;
}

function logClient(message) {
  try {
    const bridge = resolveBridge();
    if (!bridge || typeof bridge.LogClient !== "function") return;
    bridge.LogClient(`[${new Date().toISOString()}] ${message}`);
  } catch {
    // Do not interrupt UX because of logging failure.
  }
}

// ─────────────────────────────────────────
// 1. УТИЛІТИ
// ─────────────────────────────────────────

/**
 * Викликає метод C#-моста та повертає розібраний об'єкт.
 * Якщо bridge не доступний (розробка у браузері) — кидає помилку.
 */
function callBridge(method, ...args) {
  const bridge = resolveBridge();
  if (!bridge) {
    console.warn("Bridge недоступний — hostObjects не ініціалізований.");
    return { ok: false, error: "Bridge не підключений" };
  }
  try {
    const raw = bridge[method](...args);
    // Якщо повертається рядок JSON — розбираємо
    if (typeof raw === "string") return JSON.parse(raw);
    return raw; // на випадок не-JSON відповіді
  } catch (e) {
    logClient(`callBridge failed: method=${method}; error=${String(e)}`);
    return { ok: false, error: String(e) };
  }
}

/**
 * Відображає toast-повідомлення.
 * @param {string} msg   Текст
 * @param {"ok"|"err"} type
 */
function showToast(msg, type = "ok") {
  const t = document.getElementById("toast");
  t.textContent = msg;
  t.style.color = type === "ok" ? "#86efac" : "#fca5a5";
  t.style.opacity = "1";
  clearTimeout(window._toastTimer);
  window._toastTimer = setTimeout(() => { t.style.opacity = "0"; }, 2800);
}

/** Форматує число як грошову суму (грн). */
function fmt(amount) {
  return Number(amount).toLocaleString("uk-UA", {
    minimumFractionDigits: 2, maximumFractionDigits: 2
  }) + " ₴";
}

/** Форматує ISO-дату "yyyy-MM-dd" у "dd.MM.yyyy". */
function fmtDate(isoDate) {
  if (!isoDate) return "—";
  const [y, m, d] = isoDate.split("T")[0].split("-");
  return `${d}.${m}.${y}`;
}

/** Повертає сьогоднішню дату у форматі yyyy-MM-dd. */
function today() {
  return new Date().toISOString().split("T")[0];
}

/** Показує/приховує рядок помилки у формі. */
function setFormError(id, msg) {
  const el = document.getElementById(id);
  if (!el) return;
  if (msg) { el.textContent = msg; el.classList.remove("hidden"); }
  else      { el.textContent = "";  el.classList.add("hidden"); }
}

// ─────────────────────────────────────────
// 2. НАВІГАЦІЯ МІЖ ВКЛАДКАМИ
// ─────────────────────────────────────────

/** Відкриває вкладку, приховує решту. */
function openTab(name) {
  const pages = ["dashboard", "incomes", "expenses", "categories", "reports", "profile"];
  pages.forEach(p => {
    document.getElementById(`page-${p}`)?.classList.toggle("hidden", p !== name);
    document.getElementById(`tab-${p}`)?.classList.toggle("active", p === name);
  });
  // Рефреш даних при переключенні
  if (name === "dashboard")  renderDashboard();
  if (name === "incomes")    renderIncomes();
  if (name === "expenses")   renderExpenses();
  if (name === "categories") renderCategories();
  if (name === "profile")    loadProfile();
}

function closeModal(id) {
  document.getElementById(id)?.classList.add("hidden");
}

// ─────────────────────────────────────────
// 3. ДАШБОРД
// ─────────────────────────────────────────

function renderDashboard() {
  // Загальні суми
  const incomes  = getIncomes();
  const expenses = getExpenses();

  const totalIncome  = incomes .reduce((s, i) => s + i.amount, 0);
  const totalExpense = expenses.reduce((s, e) => s + e.amount, 0);
  const netProfit    = totalIncome - totalExpense;

  document.getElementById("stat-income") .textContent = fmt(totalIncome);
  document.getElementById("stat-expense").textContent = fmt(totalExpense);

  const profEl = document.getElementById("stat-profit");
  profEl.textContent = fmt(netProfit);
  profEl.style.color = netProfit >= 0 ? "#86efac" : "#fca5a5";

  // Останні 10 операцій (по даті — спадання)
  const all = [
    ...incomes .map(i => ({ ...i, type: "income"  })),
    ...expenses.map(e => ({ ...e, type: "expense" }))
  ].sort((a, b) => new Date(b.date) - new Date(a.date)).slice(0, 10);

  const tbody = document.getElementById("recentList");
  if (!all.length) {
    tbody.innerHTML = '<p class="text-xs text-center py-8" style="color:var(--muted);">Немає записів</p>';
    return;
  }

  tbody.innerHTML = `
    <table class="w-full text-sm">
      <thead><tr style="border-bottom:1px solid var(--border);color:var(--muted);" class="text-left text-xs">
        <th class="px-4 py-2">Дата</th>
        <th class="px-4 py-2">Тип</th>
        <th class="px-4 py-2">Опис</th>
        <th class="px-4 py-2 text-right">Сума</th>
      </tr></thead>
      <tbody>
        ${all.map(r => `
          <tr class="row-hover" style="border-bottom:1px solid var(--border);">
            <td class="px-4 py-2 text-xs" style="color:var(--muted);">${fmtDate(r.date)}</td>
            <td class="px-4 py-2">
              ${r.type === "income"
                ? '<span class="badge-income">Дохід</span>'
                : '<span class="badge-expense">Витрата</span>'}
            </td>
            <td class="px-4 py-2 text-xs">${escHtml(r.type === "income" ? r.source : r.categoryName)} ${r.description ? '· ' + escHtml(r.description) : ''}</td>
            <td class="px-4 py-2 text-right font-mono text-xs" style="color:${r.type === 'income' ? '#86efac' : '#fca5a5'};">
              ${r.type === "income" ? "+" : "–"}${fmt(r.amount)}
            </td>
          </tr>`).join("")}
      </tbody>
    </table>`;
}

// ─────────────────────────────────────────
// 4. ДОХОДИ
// ─────────────────────────────────────────

/** Повертає масив доходів з C# або з локального кешу. */
function getIncomes() {
  try {
    const bridge = resolveBridge();
    const raw = bridge ? bridge.GetIncomes() : "[]";
    return JSON.parse(raw) || [];
  } catch { return []; }
}

function renderIncomes() {
  const list = getIncomes();
  const tbody = document.getElementById("incomesBody");
  if (!list.length) {
    tbody.innerHTML = '<tr><td colspan="5" class="text-center py-8 text-xs" style="color:var(--muted);">Немає записів</td></tr>';
    return;
  }
  tbody.innerHTML = list
    .sort((a, b) => new Date(b.date) - new Date(a.date))
    .map(i => `
      <tr class="row-hover" style="border-bottom:1px solid var(--border);">
        <td class="px-4 py-3 text-xs" style="color:var(--muted);">${fmtDate(i.date)}</td>
        <td class="px-4 py-3 text-xs">${escHtml(i.source)}</td>
        <td class="px-4 py-3 text-xs" style="color:var(--muted);">${escHtml(i.description)}</td>
        <td class="px-4 py-3 text-right font-mono text-xs" style="color:#86efac;">+${fmt(i.amount)}</td>
        <td class="px-4 py-3 flex gap-2 justify-end">
          <button class="btn-ghost" style="font-size:.7rem;padding:.25rem .6rem;" onclick="openIncomeModal(${JSON.stringify(JSON.stringify(i))})">✏️</button>
          <button class="btn-danger"  style="font-size:.7rem;padding:.25rem .6rem;" onclick="deleteRecord('${i.id}','incomes')">🗑</button>
        </td>
      </tr>`).join("");
}

function openIncomeModal(jsonStr) {
  document.getElementById("incomeModal").classList.remove("hidden");
  setFormError("income-error", "");
  if (jsonStr) {
    // Режим редагування
    const i = JSON.parse(jsonStr);
    document.getElementById("incomeModalTitle").textContent = "Редагувати дохід";
    document.getElementById("income-id")    .value = i.id;
    document.getElementById("income-date")  .value = (i.date || "").split("T")[0];
    document.getElementById("income-source").value = i.source;
    document.getElementById("income-amount").value = i.amount;
    document.getElementById("income-desc")  .value = i.description;
  } else {
    // Новий запис
    document.getElementById("incomeModalTitle").textContent = "Новий дохід";
    document.getElementById("income-id")    .value = "";
    document.getElementById("income-date")  .value = today();
    document.getElementById("income-source").value = "";
    document.getElementById("income-amount").value = "";
    document.getElementById("income-desc")  .value = "";
  }
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
  const method  = id ? "UpdateIncome" : "AddIncome";
  const res     = callBridge(method, payload);

  if (res.ok) {
    closeModal("incomeModal");
    renderIncomes();
    renderDashboard();
    showToast(id ? "Дохід оновлено ✓" : "Дохід додано ✓");
  } else {
    setFormError("income-error", res.error);
  }
}

// ─────────────────────────────────────────
// 5. ВИТРАТИ
// ─────────────────────────────────────────

function getExpenses() {
  try {
    const bridge = resolveBridge();
    const raw = bridge ? bridge.GetExpenses() : "[]";
    return JSON.parse(raw) || [];
  } catch { return []; }
}

function renderExpenses() {
  const list = getExpenses();
  const tbody = document.getElementById("expensesBody");
  if (!list.length) {
    tbody.innerHTML = '<tr><td colspan="5" class="text-center py-8 text-xs" style="color:var(--muted);">Немає записів</td></tr>';
    return;
  }
  tbody.innerHTML = list
    .sort((a, b) => new Date(b.date) - new Date(a.date))
    .map(e => `
      <tr class="row-hover" style="border-bottom:1px solid var(--border);">
        <td class="px-4 py-3 text-xs" style="color:var(--muted);">${fmtDate(e.date)}</td>
        <td class="px-4 py-3"><span class="badge-expense">${escHtml(e.categoryName || "—")}</span></td>
        <td class="px-4 py-3 text-xs" style="color:var(--muted);">${escHtml(e.description)}</td>
        <td class="px-4 py-3 text-right font-mono text-xs" style="color:#fca5a5;">–${fmt(e.amount)}</td>
        <td class="px-4 py-3 flex gap-2 justify-end">
          <button class="btn-ghost" style="font-size:.7rem;padding:.25rem .6rem;" onclick="openExpenseModal(${JSON.stringify(JSON.stringify(e))})">✏️</button>
          <button class="btn-danger"  style="font-size:.7rem;padding:.25rem .6rem;" onclick="deleteRecord('${e.id}','expenses')">🗑</button>
        </td>
      </tr>`).join("");
}

function openExpenseModal(jsonStr) {
  // Наповнюємо дропдаун категорій
  const cats = getCategories();
  const sel  = document.getElementById("expense-category");
  sel.innerHTML = cats.map(c => `<option value="${c.id}">${escHtml(c.name)}</option>`).join("");

  document.getElementById("expenseModal").classList.remove("hidden");
  setFormError("expense-error", "");

  if (jsonStr) {
    const e = JSON.parse(jsonStr);
    document.getElementById("expenseModalTitle").textContent = "Редагувати витрату";
    document.getElementById("expense-id")      .value = e.id;
    document.getElementById("expense-date")    .value = (e.date || "").split("T")[0];
    sel.value = e.categoryId;
    document.getElementById("expense-amount")  .value = e.amount;
    document.getElementById("expense-desc")    .value = e.description;
  } else {
    document.getElementById("expenseModalTitle").textContent = "Нова витрата";
    document.getElementById("expense-id")       .value = "";
    document.getElementById("expense-date")     .value = today();
    document.getElementById("expense-amount")   .value = "";
    document.getElementById("expense-desc")     .value = "";
  }
}

function submitExpense() {
  const id         = document.getElementById("expense-id")      .value.trim();
  const date       = document.getElementById("expense-date")    .value;
  const catSel     = document.getElementById("expense-category");
  const categoryId = catSel.value;
  const catName    = catSel.options[catSel.selectedIndex]?.text || "";
  const amount     = parseFloat(document.getElementById("expense-amount").value);
  const desc       = document.getElementById("expense-desc").value.trim();

  if (!date || !categoryId || isNaN(amount) || amount <= 0) {
    setFormError("expense-error", "Заповніть усі обов'язкові поля. Сума має бути > 0.");
    return;
  }

  const payload = JSON.stringify({
    id: id || undefined, date, categoryId, categoryName: catName, amount, description: desc
  });
  const method = id ? "UpdateExpense" : "AddExpense";
  const res    = callBridge(method, payload);

  if (res.ok) {
    closeModal("expenseModal");
    renderExpenses();
    renderDashboard();
    showToast(id ? "Витрату оновлено ✓" : "Витрату додано ✓");
  } else {
    setFormError("expense-error", res.error);
  }
}

// ─────────────────────────────────────────
// 6. ВИДАЛЕННЯ (доходи / витрати)
// ─────────────────────────────────────────

function deleteRecord(id, context) {
  if (!confirm("Видалити запис?")) return;
  const res = callBridge("RemoveRecord", id);
  if (res.ok) {
    if (context === "incomes")   renderIncomes();
    if (context === "expenses")  renderExpenses();
    renderDashboard();
    showToast("Запис видалено");
  } else {
    showToast(res.error, "err");
  }
}

// ─────────────────────────────────────────
// 7. КАТЕГОРІЇ
// ─────────────────────────────────────────

function getCategories() {
  try {
    const bridge = resolveBridge();
    const raw = bridge ? bridge.GetCategories() : "[]";
    return JSON.parse(raw) || [];
  } catch { return []; }
}

function renderCategories() {
  const list = getCategories();
  const grid = document.getElementById("categoriesGrid");

  if (!list.length) {
    grid.innerHTML = '<p class="text-xs col-span-3 py-8 text-center" style="color:var(--muted);">Немає категорій. Додайте першу!</p>';
    return;
  }

  grid.innerHTML = list.map(c => `
    <div class="card p-4 flex flex-col gap-2">
      <div class="font-semibold text-sm">${escHtml(c.name)}</div>
      <div class="text-xs flex-1" style="color:var(--muted);">${escHtml(c.description || "")}</div>
      <div class="flex gap-2 pt-1">
        <button class="btn-ghost flex-1" style="font-size:.7rem;" onclick="openCategoryModal(${JSON.stringify(JSON.stringify(c))})">✏️ Ред.</button>
        <button class="btn-danger"       style="font-size:.7rem;" onclick="deleteCategory('${c.id}')">🗑</button>
      </div>
    </div>`).join("");
}

function openCategoryModal(jsonStr) {
  document.getElementById("categoryModal").classList.remove("hidden");
  setFormError("cat-error", "");
  if (jsonStr) {
    const c = JSON.parse(jsonStr);
    document.getElementById("categoryModalTitle").textContent = "Редагувати категорію";
    document.getElementById("cat-id")  .value = c.id;
    document.getElementById("cat-name").value = c.name;
    document.getElementById("cat-desc").value = c.description;
  } else {
    document.getElementById("categoryModalTitle").textContent = "Нова категорія";
    document.getElementById("cat-id")  .value = "";
    document.getElementById("cat-name").value = "";
    document.getElementById("cat-desc").value = "";
  }
}

function submitCategory() {
  const id   = document.getElementById("cat-id")  .value.trim();
  const name = document.getElementById("cat-name").value.trim();
  const desc = document.getElementById("cat-desc").value.trim();

  if (!name) { setFormError("cat-error", "Назва є обов'язковою."); return; }

  const payload = JSON.stringify({ id: id || undefined, name, description: desc });
  const method  = id ? "UpdateCategory" : "AddCategory";
  const res     = callBridge(method, payload);

  if (res.ok) {
    closeModal("categoryModal");
    renderCategories();
    showToast(id ? "Категорію оновлено ✓" : "Категорію додано ✓");
  } else {
    setFormError("cat-error", res.error);
  }
}

function deleteCategory(id) {
  if (!confirm("Видалити категорію?")) return;
  const res = callBridge("RemoveCategory", id);
  if (res.ok) { renderCategories(); showToast("Категорію видалено"); }
  else        { showToast(res.error, "err"); }
}

// ─────────────────────────────────────────
// 8. ЗВІТИ
// ─────────────────────────────────────────

// Зберігаємо останній звіт для подальшого експорту
let _lastReport = null;

function generateReport() {
  const start = document.getElementById("reportStart").value;
  const end   = document.getElementById("reportEnd")  .value;

  if (!start || !end) { showToast("Вкажіть обидві дати", "err"); return; }
  if (start > end)    { showToast("Початок не може бути пізніше кінця", "err"); return; }

  const bridge = resolveBridge();
  const raw = bridge ? bridge.GenerateReport(start, end) : "{}";
  let report;
  try { report = JSON.parse(raw); } catch { showToast("Помилка формування звіту", "err"); return; }

  _lastReport = report;

  document.getElementById("rep-income") .textContent = fmt(report.totalIncome  || 0);
  document.getElementById("rep-expense").textContent = fmt(report.totalExpense || 0);

  const profit   = (report.totalIncome || 0) - (report.totalExpense || 0);
  const profEl   = document.getElementById("rep-profit");
  profEl.textContent = fmt(profit);
  profEl.style.color = profit >= 0 ? "#86efac" : "#fca5a5";

  // Таблиця доходів
  document.getElementById("rep-incomes-body").innerHTML =
    (report.incomes || []).map(i =>
      `<tr style="border-bottom:1px solid var(--border);">
        <td class="px-3 py-2">${fmtDate(i.date)}</td>
        <td class="px-3 py-2">${escHtml(i.source)}</td>
        <td class="px-3 py-2 text-right font-mono" style="color:#86efac;">+${fmt(i.amount)}</td>
      </tr>`).join("") || `<tr><td colspan="3" class="px-3 py-4 text-center" style="color:var(--muted);">Немає</td></tr>`;

  // Таблиця витрат
  document.getElementById("rep-expenses-body").innerHTML =
    (report.expenses || []).map(e =>
      `<tr style="border-bottom:1px solid var(--border);">
        <td class="px-3 py-2">${fmtDate(e.date)}</td>
        <td class="px-3 py-2">${escHtml(e.categoryName)}</td>
        <td class="px-3 py-2 text-right font-mono" style="color:#fca5a5;">–${fmt(e.amount)}</td>
      </tr>`).join("") || `<tr><td colspan="3" class="px-3 py-4 text-center" style="color:var(--muted);">Немає</td></tr>`;

  document.getElementById("reportResult").classList.remove("hidden");
}

function exportReport(format) {
  if (!_lastReport) { showToast("Спочатку сформуйте звіт", "err"); return; }
  const res = callBridge("ExportReport", JSON.stringify(_lastReport), format);
  if (res.ok && res.data !== "cancelled")
    showToast(`Звіт збережено: ${res.data}`);
  else if (res.error)
    showToast(res.error, "err");
}

// ─────────────────────────────────────────
// 9. ПРОФІЛЬ ФОП
// ─────────────────────────────────────────

function loadProfile() {
  const bridge = resolveBridge();
  const raw = bridge ? bridge.GetEntrepreneur() : "{}";
  let e;
  try { e = JSON.parse(raw); } catch { return; }

  document.getElementById("prof-fullName") .value = e.fullName           || "";
  document.getElementById("prof-taxGroup") .value = String(e.taxGroup    || 3);
  document.getElementById("prof-regNumber").value = e.registrationNumber || "";

  updateEntrepreneurLabel(e);
}

function saveProfile() {
  const e = {
    fullName:           document.getElementById("prof-fullName") .value.trim(),
    taxGroup:           parseInt(document.getElementById("prof-taxGroup").value),
    registrationNumber: document.getElementById("prof-regNumber").value.trim()
  };
  const res = callBridge("SaveEntrepreneur", JSON.stringify(e));
  if (res.ok) { updateEntrepreneurLabel(e); showToast("Профіль збережено ✓"); }
  else        { showToast(res.error, "err"); }
}

function updateEntrepreneurLabel(e) {
  const lbl = document.getElementById("entrepreneurLabel");
  if (e && e.fullName)
    lbl.textContent = `${e.fullName} | Група ${e.taxGroup} | ${e.registrationNumber}`;
}

// ─────────────────────────────────────────
// 10. ЗБЕРЕЖЕННЯ / ЗАВАНТАЖЕННЯ
// ─────────────────────────────────────────

function saveData(format) {
  const res = callBridge("SaveData", format);
  if (res.ok && res.data !== "cancelled")
    showToast(`Дані збережено ✓ (${res.data})`);
  else if (res.error)
    showToast(res.error, "err");
}

function loadData() {
  const res = callBridge("LoadData");
  if (res.ok && res.data === "loaded") {
    renderDashboard();
    loadProfile();
    showToast("Дані завантажено ✓");
  } else if (res.error) {
    showToast(res.error, "err");
  }
}

// ─────────────────────────────────────────
// ДОПОМІЖНІ
// ─────────────────────────────────────────

/** Екранує HTML-спецсимволи для безпечного рендерингу. */
function escHtml(str) {
  if (!str) return "";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

// ─────────────────────────────────────────
// ІНІЦІАЛІЗАЦІЯ
// ─────────────────────────────────────────

document.addEventListener("DOMContentLoaded", () => {
  window.addEventListener("error", (ev) => {
    logClient(`window.onerror: ${ev.message} at ${ev.filename}:${ev.lineno}:${ev.colno}`);
  });
  window.addEventListener("unhandledrejection", (ev) => {
    logClient(`unhandledrejection: ${String(ev.reason)}`);
  });

  const bridge = resolveBridge();
  if (!bridge) {
    showToast("Bridge не підключений. Перевірте backend-логи.", "err");
    return;
  }

  const ping = callBridge("Ping");
  if (!ping.ok) {
    showToast("Bridge відповів з помилкою. Деталі в логах.", "err");
    return;
  }
  logClient("Bridge handshake OK");

  // Встановлюємо поточний місяць за замовчуванням у звітах
  const now   = new Date();
  const year  = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const lastDay = new Date(year, now.getMonth() + 1, 0).getDate();

  document.getElementById("reportStart").value = `${year}-${month}-01`;
  document.getElementById("reportEnd")  .value = `${year}-${month}-${lastDay}`;

  // Початкове завантаження
  loadProfile();
  renderDashboard();
});
