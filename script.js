/* ==========================================================================
   CONFIG
   ========================================================================== */
const ADMIN_PASSWORD = "admin123";
const STORAGE_KEY = "glv-appointment-data-v1";
const DEFAULT_LOGO = "assets/logo.png";

/* ==========================================================================
   DEFAULT DATA
   ========================================================================== */
const defaultData = {
  groupName: "Đoàn TNTT Têrêsa Hài Đồng Giêsu",
  parishName: "Giáo xứ Suối Nho",
  schoolYear: "2026 – 2027",
  logo: "",
  message: "Xin Thiên Chúa nâng đỡ và ban ơn, giúp Trưởng luôn hăng say phục vụ.",
  classes: [
    {
      id: "khai-tam",
      name: "Khai tâm",
      color: "#E9A0B7",
      teachers: [
        { id: "teacher-1", name: "Maria Trần Thị Thuý Kiều" },
        { id: "teacher-2", name: "Maria Đào Hồng Quyên" }
      ]
    },
    {
      id: "den-ban-tiec-thanh-1",
      name: "Đến Bàn Tiệc Thánh 1",
      color: "#41AD49",
      teachers: [
        { id: "teacher-3", name: "Maria Phạm Minh Thư" },
        { id: "teacher-4", name: "Maria Nguyễn Khánh Ngọc" },
        { id: "teacher-5", name: "Maria Trần Thị Cẩm Ly" }
      ]
    },
    {
      id: "den-ban-tiec-thanh-3",
      name: "Đến Bàn Tiệc Thánh 3",
      color: "#0260A2",
      teachers: [
        { id: "teacher-6", name: "Maria Đào Thảo Vi" },
        { id: "teacher-7", name: "Maria Nguyễn Thuý Mai" }
      ]
    },
    {
      id: "lon-len-trong-cttt-2",
      name: "Lớn Lên Trong Chúa Thánh Thần 2",
      color: "#D9A441",
      teachers: [
        { id: "teacher-8", name: "Maria Phạm Thị Yến Vi" },
        { id: "teacher-9", name: "Maria Phạm Yến Nhi" },
      ]
    },
    {
      id: "song-dao-1",
      name: "Sống Đạo 1",
      color: "#843905",
      teachers: [
        { id: "teacher-11", name: "Maria Phạm Kiều Trang" },
        { id: "teacher-12", name: "Giuse Trần Quốc Cường" }
      ]
    },
    {
      id: "song-dao-3",
      name: "Sống Đạo 3",
      color: "#DB0810",
      teachers: [
        { id: "teacher-13", name: "Vinhsơn Đào Văn Thắng" },
        { id: "teacher-14", name: "Maria Trương Thanh Chúc" }
      ]
    }
  ]
};

/* ==========================================================================
   STATE
   ========================================================================== */
let data = null;
let currentView = "home"; // home | class | teacher | decision | success | admin-login | admin
let selectedClassId = null;
let selectedTeacherId = null;
let adminAuthed = false;
let adminEditingClassId = null; // which class's teacher-list is expanded in admin

/* ==========================================================================
   PERSISTENCE
   ========================================================================== */
function loadData() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (parsed && Array.isArray(parsed.classes)) {
        data = parsed;
        return data;
      }
    }
  } catch (e) {
    console.warn("Không đọc được dữ liệu đã lưu, dùng dữ liệu mặc định.", e);
  }
  data = deepClone(defaultData);
  saveData();
  return data;
}

function saveData() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}

function deepClone(obj) {
  return JSON.parse(JSON.stringify(obj));
}

/* ==========================================================================
   HELPERS
   ========================================================================== */
function esc(str) {
  return String(str == null ? "" : str).replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  }[c]));
}

function uid(prefix) {
  return prefix + "-" + Date.now() + "-" + Math.floor(Math.random() * 10000);
}

function getClass(classId) {
  return data.classes.find((c) => c.id === classId) || null;
}

function getTeacher(classId, teacherId) {
  const cls = getClass(classId);
  if (!cls) return null;
  return cls.teachers.find((t) => t.id === teacherId) || null;
}

function getTeacherIndex(classId, teacherId) {
  const cls = getClass(classId);
  if (!cls) return -1;
  return cls.teachers.findIndex((t) => t.id === teacherId);
}

function getLogoSrc() {
  return data.logo && data.logo.length > 0 ? data.logo : DEFAULT_LOGO;
}

function hexToRgb(hex) {
  const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex || "");
  if (!m) return "36, 90, 120";
  return [parseInt(m[1], 16), parseInt(m[2], 16), parseInt(m[3], 16)].join(", ");
}

function setClassTheme(color) {
  const root = document.documentElement;
  root.style.setProperty("--class-color", color);
  root.style.setProperty("--class-color-rgb", hexToRgb(color));
}

function pad2(n) {
  return String(n).padStart(2, "0");
}

/* ==========================================================================
   TOAST
   ========================================================================== */
function showToast(message) {
  const root = document.getElementById("toast-root");
  const el = document.createElement("div");
  el.className = "toast";
  el.textContent = message;
  root.appendChild(el);
  setTimeout(() => el.remove(), 3000);
}

/* ==========================================================================
   MODAL
   ========================================================================== */
function showModal(innerHtml) {
  const root = document.getElementById("modal-root");
  root.innerHTML = `<div class="modal-overlay" data-action="close-modal">
    <div class="modal-box" data-stop>${innerHtml}</div>
  </div>`;
}

function closeModal() {
  document.getElementById("modal-root").innerHTML = "";
}

function openConfirmDialog({ title, message, confirmLabel, onConfirm, danger }) {
  showModal(`
    <div class="modal-title">${esc(title)}</div>
    <div class="modal-summary"><div class="modal-summary-row" style="justify-content:center;text-align:center;">${esc(message)}</div></div>
    <button class="btn-secondary" data-action="close-modal">HỦY</button>
    <button class="btn-primary" style="margin-top:10px;${danger ? "background:#a83d45;box-shadow:none;" : ""}" data-action="run-confirm">${esc(confirmLabel || "XÁC NHẬN")}</button>
  `);
  window.__pendingConfirm = onConfirm;
}

/* ==========================================================================
   MASTER RENDER
   ========================================================================== */
const SCREEN_TRANSITION_VARIANT = {
  teacher: "scale",
  success: "scale"
};
const TRANSITION_EXIT_MS = 220;

function buildScreenHtml() {
  switch (currentView) {
    case "home": return renderHome();
    case "class": return renderClassList();
    case "teacher": return renderEnvelope();
    case "decision": return renderDecision();
    case "success": return renderSuccess();
    case "admin-login": return renderAdminLogin();
    case "admin": return renderAdminPanel();
    default: return renderHome();
  }
}

function mountScreen(app, html, variant) {
  app.innerHTML = html;
  const incoming = app.firstElementChild;
  if (incoming) {
    const startClass = variant === "scale" ? "va-scale-start" : "va-slide-start";
    incoming.classList.add("view-anim", startClass);
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        incoming.classList.remove(startClass);
        incoming.classList.add("va-visible");
      });
    });
  }
  window.scrollTo(0, 0);
}

function render() {
  const app = document.getElementById("app");
  const fab = document.getElementById("admin-fab");
  fab.classList.toggle("is-hidden", currentView !== "home");

  const html = buildScreenHtml();
  const variant = SCREEN_TRANSITION_VARIANT[currentView] || "slide";
  const outgoing = app.firstElementChild;

  if (outgoing) {
    const exitClass = variant === "scale" ? "va-scale-exit" : "va-slide-exit";
    outgoing.classList.add("view-anim");
    outgoing.classList.remove("va-visible");
    outgoing.classList.add(exitClass);
    setTimeout(() => mountScreen(app, html, variant), TRANSITION_EXIT_MS);
  } else {
    mountScreen(app, html, variant);
  }
}

/* ==========================================================================
   SCREEN 1 — HOME (6 CLASSES)
   ========================================================================== */
function showHome() {
  currentView = "home";
  selectedClassId = null;
  selectedTeacherId = null;
  render();
}

function renderHome() {
  const cards = data.classes.map((cls, idx) => `
    <button class="class-card" style="--card-color:${esc(cls.color)}" data-action="open-class" data-class-id="${esc(cls.id)}">
      <div class="class-card-row">
        <div class="class-card-index">${pad2(idx + 1)}</div>
        <div class="class-card-body">
          <div class="class-card-name">${esc(cls.name)}</div>
          <div class="class-card-count">${cls.teachers.length} Giáo lý viên</div>
        </div>
        <div class="class-card-arrow">→</div>
      </div>
    </button>
  `).join("");

  return `
    <div class="screen">
      <div class="top-header">
        <div class="logo-wrap"><img src="${esc(getLogoSrc())}" alt="Logo" /></div>
        <div class="header-eyebrow">${esc(data.groupName)}</div>
        <div class="header-parish">${esc(data.parishName)}</div>
        <div class="header-divider"></div>
        <div class="header-title">TRAO QUYẾT ĐỊNH BỔ NHIỆM</div>
        <div class="header-year">NĂM HỌC ${esc(data.schoolYear)}</div>
      </div>
      <div class="class-grid">${cards}</div>
    </div>
  `;
}

/* ==========================================================================
   SCREEN 2 — TEACHER LIST OF A CLASS
   ========================================================================== */
function showClass(classId) {
  const cls = getClass(classId);
  if (!cls) return showHome();
  selectedClassId = classId;
  selectedTeacherId = null;
  currentView = "class";
  setClassTheme(cls.color);
  render();
}

function renderClassList() {
  const cls = getClass(selectedClassId);
  if (!cls) return renderHome();

  const rows = cls.teachers.map((t, idx) => `
    <button class="teacher-card" data-action="open-teacher" data-class-id="${esc(cls.id)}" data-teacher-id="${esc(t.id)}">
      <span class="teacher-card-index">${pad2(idx + 1)}</span>
      <span class="teacher-card-body">
        <span class="teacher-card-label">Giáo lý viên ${idx + 1}</span>
        <span class="teacher-card-hint">Nhấn để nhận thư bổ nhiệm</span>
      </span>
      <span class="teacher-card-arrow">→</span>
    </button>
  `).join("");

  const body = cls.teachers.length
    ? `<div class="teacher-list">${rows}</div>`
    : `<div class="empty-state">Lớp này chưa có Giáo lý viên.</div>`;

  return `
    <div class="screen">
      <button class="back-btn" data-action="go-home">← Quay lại</button>
      <div class="top-header">
        <div class="logo-wrap"><img src="${esc(getLogoSrc())}" alt="Logo" /></div>
        <div class="header-eyebrow">${esc(data.groupName)}</div>
      </div>
      <div class="class-label">${esc(cls.name).toUpperCase()}</div>
      <div class="class-sublabel">Giáo lý viên đứng lớp</div>
      ${body}
    </div>
  `;
}

/* ==========================================================================
   SCREEN 3 — ENVELOPE
   ========================================================================== */
function showTeacher(classId, teacherId) {
  const cls = getClass(classId);
  const teacher = getTeacher(classId, teacherId);
  if (!cls || !teacher) return showHome();
  selectedClassId = classId;
  selectedTeacherId = teacherId;
  currentView = "teacher";
  setClassTheme(cls.color);
  render();
}

function renderEnvelope() {
  const cls = getClass(selectedClassId);
  if (!cls) return renderHome();

  return `
    <div class="screen envelope-screen">
      <button class="back-btn" data-action="back-to-class" data-class-id="${esc(cls.id)}">← Quay lại</button>
      <div class="envelope-stage">
        <div class="envelope-3d" id="envelope" data-action="open-envelope">
          <div class="env-flap"></div>
          <div class="env-seal">
            <div class="env-seal-cross">✝</div>
            <div class="env-seal-text">TNTT<br/>TÊRÊSA</div>
          </div>
          <div class="env-paper">
            <div class="env-paper-lines"><span></span><span></span><span></span></div>
          </div>
          <div class="env-body"></div>
          <div class="env-content">
            <div class="env-mini-logo"><img src="${esc(getLogoSrc())}" alt="" /></div>
            <div class="env-title-1">Đoàn TNTT</div>
            <div class="env-title-2">Têrêsa Hài Đồng Giêsu</div>
            <div class="env-subject">Thư bổ nhiệm lớp</div>
            <div class="env-parish">${esc(data.parishName)}</div>
          </div>
        </div>
      </div>
      <div class="envelope-hint">Nhấn vào phong thư để mở</div>
    </div>
  `;
}

function openEnvelope() {
  const el = document.getElementById("envelope");
  if (!el || el.classList.contains("opening")) return;
  el.classList.add("no-float");
  el.classList.add("opening");
  setTimeout(() => {
    showAppointment();
  }, 1450);
}

/* ==========================================================================
   SCREEN 4 — DECISION / APPOINTMENT
   ========================================================================== */
function showAppointment() {
  currentView = "decision";
  render();
}

function renderDecision() {
  const cls = getClass(selectedClassId);
  const teacher = getTeacher(selectedClassId, selectedTeacherId);
  if (!cls || !teacher) return renderHome();

  return `
    <div class="screen decision-screen">
      <button class="back-btn" data-action="back-to-teacher" data-class-id="${esc(cls.id)}" data-teacher-id="${esc(teacher.id)}">← Quay lại</button>
      <div class="certificate">
        <div class="cert-ornament-top">✦ ✦ ✦</div>
        <div class="cert-header">
          <div class="cert-logo"><img src="${esc(getLogoSrc())}" alt="Logo" /></div>
          <div class="cert-group">${esc(data.groupName)}</div>
          <div class="cert-parish">${esc(data.parishName)}</div>
        </div>
        <div class="cert-title">QUYẾT ĐỊNH BỔ NHIỆM</div>
        <div class="cert-body">
          <div class="cert-role">Trưởng</div>
          <div class="cert-name">${esc(teacher.name).toUpperCase()}</div>
          <div class="cert-line">được bổ nhiệm đảm nhiệm</div>
          <div class="cert-role" style="margin-top:10px;">Lớp</div>
          <div class="cert-class-name">${esc(cls.name).toUpperCase()}</div>
          <div class="cert-role">Năm học</div>
          <div class="cert-year">${esc(data.schoolYear)}</div>
          <div class="cert-quote">“${esc(data.message)}”</div>
        </div>
        <div class="cert-ornament-bottom" style="margin-top:14px;">✦ ✦ ✦</div>
        <div class="cert-actions">
          <button class="btn-primary" data-action="open-confirm-modal">XÁC NHẬN BỔ NHIỆM</button>
        </div>
      </div>
    </div>
  `;
}

function openConfirmAppointmentModal() {
  const cls = getClass(selectedClassId);
  const teacher = getTeacher(selectedClassId, selectedTeacherId);
  if (!cls || !teacher) return;

  showModal(`
    <div class="modal-title">XÁC NHẬN BỔ NHIỆM</div>
    <div class="modal-summary">
      <div class="modal-summary-role">Trưởng</div>
      <div class="modal-summary-name">${esc(teacher.name)}</div>
      <div class="modal-summary-row"><span>Lớp</span><b>${esc(cls.name)}</b></div>
      <div class="modal-summary-row"><span>Năm học</span><b>${esc(data.schoolYear)}</b></div>
    </div>
    <button class="btn-secondary" data-action="close-modal">QUAY LẠI</button>
    <button class="btn-primary" style="margin-top:10px;" data-action="confirm-appointment">XÁC NHẬN</button>
  `);
}

function confirmAppointment() {
  closeModal();
  showSuccess();
}

/* ==========================================================================
   SCREEN 5 — SUCCESS
   ========================================================================== */
function showSuccess() {
  currentView = "success";
  render();
}

function renderSuccess() {
  const cls = getClass(selectedClassId);
  const teacher = getTeacher(selectedClassId, selectedTeacherId);
  if (!cls || !teacher) return renderHome();

  return `
    <div class="screen success-screen">
      <div class="success-check">✓</div>
      <div class="success-title">ĐÃ XÁC NHẬN BỔ NHIỆM</div>
      <div class="success-role">Trưởng</div>
      <div class="success-name">${esc(teacher.name).toUpperCase()}</div>
      <div class="success-class">${esc(cls.name)}</div>
      <div class="success-blessing">Nguyện xin Chúa ban ơn và đồng hành cùng Trưởng trong sứ vụ mới.</div>
      <button class="btn-primary" data-action="go-home">VỀ TRANG CHỦ</button>
    </div>
  `;
}

/* ==========================================================================
   ADMIN — AUTH
   ========================================================================== */
function openAdmin() {
  currentView = adminAuthed ? "admin" : "admin-login";
  render();
}

function renderAdminLogin() {
  return `
    <div class="admin-screen">
      <div class="admin-body">
        <div class="admin-login-box">
          <div class="logo-wrap"><img src="${esc(getLogoSrc())}" alt="Logo" /></div>
          <div class="header-title" style="font-size:17px;margin-bottom:16px;">KHU VỰC QUẢN LÝ</div>
          <div class="form-group" style="text-align:left;">
            <label class="form-label">Mật khẩu quản trị</label>
            <input type="password" class="form-input" id="admin-password-input" placeholder="Nhập mật khẩu" />
          </div>
          <button class="btn-primary" data-action="login-admin">ĐĂNG NHẬP</button>
          <button class="btn-secondary" data-action="go-home">QUAY LẠI TRANG CHỦ</button>
        </div>
      </div>
    </div>
  `;
}

function loginAdmin() {
  const input = document.getElementById("admin-password-input");
  const val = input ? input.value : "";
  if (val === ADMIN_PASSWORD) {
    adminAuthed = true;
    currentView = "admin";
    render();
    showToast("Đăng nhập quản trị thành công");
  } else {
    showToast("Sai mật khẩu, vui lòng thử lại");
  }
}

/* ==========================================================================
   ADMIN — PANEL
   ========================================================================== */
function renderAdminPanel() {
  return `
    <div class="admin-screen">
      <div class="admin-header">
        <button class="back-btn" data-action="go-home">← Trang chủ</button>
        <div class="admin-header-title">⚙ Quản lý</div>
      </div>
      <div class="admin-body">
        ${renderAdminGeneralSection()}
        ${renderAdminLogoSection()}
        ${renderAdminClassesSection()}
        ${renderAdminDataSection()}
      </div>
    </div>
  `;
}

function renderAdminGeneralSection() {
  return `
    <div class="admin-section">
      <div class="admin-section-title">Thông tin chung</div>
      <div class="form-group">
        <label class="form-label">Tên Xứ đoàn</label>
        <input class="form-input" id="input-group-name" value="${esc(data.groupName)}" />
      </div>
      <div class="form-group">
        <label class="form-label">Giáo xứ</label>
        <input class="form-input" id="input-parish-name" value="${esc(data.parishName)}" />
      </div>
      <div class="form-group">
        <label class="form-label">Năm học</label>
        <input class="form-input" id="input-school-year" value="${esc(data.schoolYear)}" />
      </div>
      <div class="form-group">
        <label class="form-label">Câu lời nhắn (trong quyết định)</label>
        <textarea class="form-textarea" id="input-message">${esc(data.message)}</textarea>
      </div>
      <button class="btn-primary" data-action="save-settings">LƯU THÔNG TIN CHUNG</button>
    </div>
  `;
}

function renderAdminLogoSection() {
  return `
    <div class="admin-section">
      <div class="admin-section-title">Logo</div>
      <div class="admin-logo-preview"><img src="${esc(getLogoSrc())}" alt="Logo" /></div>
      <input type="file" accept="image/*" class="hidden-file-input" id="logo-file-input" />
      <button class="btn-outline" data-action="pick-logo-file">TẢI LOGO MỚI</button>
      ${data.logo ? `<button class="btn-outline" style="margin-top:8px;" data-action="reset-logo">DÙNG LOGO MẶC ĐỊNH</button>` : ""}
    </div>
  `;
}

function renderAdminClassesSection() {
  const items = data.classes.map((cls) => {
    const expanded = adminEditingClassId === cls.id;
    const teacherRows = cls.teachers.map((t, idx) => `
      <div class="admin-teacher-row">
        <div class="admin-teacher-info">
          <div class="admin-teacher-label">Giáo lý viên ${idx + 1}</div>
          <div class="admin-teacher-name">${esc(t.name)}</div>
        </div>
        <div class="icon-btn-row">
          <button class="icon-btn" data-action="edit-teacher" data-class-id="${esc(cls.id)}" data-teacher-id="${esc(t.id)}">Sửa</button>
          <button class="icon-btn danger" data-action="delete-teacher" data-class-id="${esc(cls.id)}" data-teacher-id="${esc(t.id)}">Xóa</button>
        </div>
      </div>
    `).join("");

    return `
      <div class="admin-class-item">
        <div class="admin-class-item-head">
          <div class="admin-class-swatch" style="background:${esc(cls.color)}"></div>
          <div class="admin-class-name">${esc(cls.name)}</div>
          <div class="admin-class-count">${cls.teachers.length} GLV</div>
        </div>
        <div class="icon-btn-row" style="margin-top:10px;">
          <button class="icon-btn" data-action="edit-class" data-class-id="${esc(cls.id)}">Sửa lớp</button>
          <button class="icon-btn danger" data-action="delete-class" data-class-id="${esc(cls.id)}">Xóa lớp</button>
          <button class="link-btn" style="margin-left:auto;" data-action="toggle-class-teachers" data-class-id="${esc(cls.id)}">${expanded ? "Thu gọn ▲" : "Xem GLV ▼"}</button>
        </div>
        ${expanded ? `
          <div style="margin-top:6px;">
            ${teacherRows || `<div class="field-hint" style="margin-top:10px;">Chưa có Giáo lý viên nào.</div>`}
            <button class="add-btn" data-action="add-teacher" data-class-id="${esc(cls.id)}">+ THÊM GIÁO LÝ VIÊN</button>
          </div>
        ` : ""}
      </div>
    `;
  }).join("");

  return `
    <div class="admin-section">
      <div class="admin-section-title">Quản lý lớp &amp; Giáo lý viên</div>
      ${items}
      <button class="add-btn" data-action="add-class">+ THÊM LỚP MỚI</button>
    </div>
  `;
}

function renderAdminDataSection() {
  return `
    <div class="admin-section">
      <div class="admin-section-title">Dữ liệu</div>
      <div class="admin-actions-grid">
        <button class="btn-outline" data-action="export-data">XUẤT DỮ LIỆU (JSON)</button>
        <button class="btn-outline" data-action="pick-import-file">NHẬP DỮ LIỆU (JSON)</button>
        <input type="file" accept="application/json" class="hidden-file-input" id="import-file-input" />
        <button class="btn-outline danger" data-action="reset-data">KHÔI PHỤC DỮ LIỆU MẶC ĐỊNH</button>
      </div>
    </div>
  `;
}

function toggleClassTeachers(classId) {
  adminEditingClassId = adminEditingClassId === classId ? null : classId;
  render();
}

/* ==========================================================================
   ADMIN — SETTINGS
   ========================================================================== */
function saveSettings() {
  const groupName = document.getElementById("input-group-name").value.trim();
  const parishName = document.getElementById("input-parish-name").value.trim();
  const schoolYear = document.getElementById("input-school-year").value.trim();
  const message = document.getElementById("input-message").value.trim();

  if (!groupName || !parishName || !schoolYear) {
    showToast("Vui lòng nhập đầy đủ thông tin");
    return;
  }

  data.groupName = groupName;
  data.parishName = parishName;
  data.schoolYear = schoolYear;
  data.message = message;
  saveData();
  showToast("Đã lưu thông tin chung");
  render();
}

/* ==========================================================================
   ADMIN — LOGO
   ========================================================================== */
function pickLogoFile() {
  document.getElementById("logo-file-input").click();
}

function handleLogoFileChange(file) {
  if (!file) return;
  if (!file.type.startsWith("image/")) {
    showToast("Vui lòng chọn file hình ảnh");
    return;
  }
  const reader = new FileReader();
  reader.onload = () => {
    data.logo = reader.result;
    saveData();
    showToast("Đã cập nhật logo");
    render();
  };
  reader.readAsDataURL(file);
}

function resetLogo() {
  data.logo = "";
  saveData();
  showToast("Đã dùng logo mặc định");
  render();
}

/* ==========================================================================
   ADMIN — CLASSES
   ========================================================================== */
function addClass() {
  showModal(`
    <div class="modal-title">Thêm lớp mới</div>
    <div class="form-group">
      <label class="form-label">Tên lớp</label>
      <input class="form-input" id="modal-class-name" placeholder="Ví dụ: Thêm Sức 1" />
    </div>
    <div class="form-group">
      <label class="form-label">Màu sắc</label>
      <div class="form-color-row">
        <input type="color" id="modal-class-color" value="#245A78" />
        <span class="field-hint" style="margin:0;">Chọn màu đại diện cho lớp</span>
      </div>
    </div>
    <button class="btn-secondary" data-action="close-modal">HỦY</button>
    <button class="btn-primary" style="margin-top:10px;" data-action="save-new-class">THÊM LỚP</button>
  `);
}

function saveNewClass() {
  const name = document.getElementById("modal-class-name").value.trim();
  const color = document.getElementById("modal-class-color").value;
  if (!name) {
    showToast("Vui lòng nhập tên lớp");
    return;
  }
  data.classes.push({ id: uid("class"), name, color, teachers: [] });
  saveData();
  closeModal();
  render();
  showToast("Đã thêm lớp mới");
}

function editClass(classId) {
  const cls = getClass(classId);
  if (!cls) return;
  showModal(`
    <div class="modal-title">Sửa lớp</div>
    <div class="form-group">
      <label class="form-label">Tên lớp</label>
      <input class="form-input" id="modal-class-name" value="${esc(cls.name)}" />
    </div>
    <div class="form-group">
      <label class="form-label">Màu sắc</label>
      <div class="form-color-row">
        <input type="color" id="modal-class-color" value="${esc(cls.color)}" />
        <span class="field-hint" style="margin:0;">Chọn màu đại diện cho lớp</span>
      </div>
    </div>
    <button class="btn-secondary" data-action="close-modal">HỦY</button>
    <button class="btn-primary" style="margin-top:10px;" data-action="save-edit-class" data-class-id="${esc(cls.id)}">LƯU THAY ĐỔI</button>
  `);
}

function saveEditClass(classId) {
  const cls = getClass(classId);
  if (!cls) return;
  const name = document.getElementById("modal-class-name").value.trim();
  const color = document.getElementById("modal-class-color").value;
  if (!name) {
    showToast("Vui lòng nhập tên lớp");
    return;
  }
  cls.name = name;
  cls.color = color;
  saveData();
  closeModal();
  render();
  showToast("Đã cập nhật lớp");
}

function deleteClass(classId) {
  const cls = getClass(classId);
  if (!cls) return;
  openConfirmDialog({
    title: "Xóa lớp",
    message: `Bạn có chắc muốn xóa lớp "${cls.name}" và toàn bộ Giáo lý viên trong lớp?`,
    confirmLabel: "XÓA LỚP",
    danger: true,
    onConfirm: () => {
      data.classes = data.classes.filter((c) => c.id !== classId);
      saveData();
      closeModal();
      render();
      showToast("Đã xóa lớp");
    }
  });
}

/* ==========================================================================
   ADMIN — TEACHERS
   ========================================================================== */
function addTeacher(classId) {
  showModal(`
    <div class="modal-title">Thêm Giáo lý viên</div>
    <div class="form-group">
      <label class="form-label">Họ tên (hiển thị trong quyết định)</label>
      <input class="form-input" id="modal-teacher-name" placeholder="Ví dụ: Maria Nguyễn Thị A" />
    </div>
    <div class="field-hint">Tên thật chỉ hiển thị trong quyết định bổ nhiệm, không hiển thị ở danh sách công khai.</div>
    <button class="btn-secondary" data-action="close-modal">HỦY</button>
    <button class="btn-primary" style="margin-top:10px;" data-action="save-new-teacher" data-class-id="${esc(classId)}">THÊM</button>
  `);
}

function saveNewTeacher(classId) {
  const cls = getClass(classId);
  if (!cls) return;
  const name = document.getElementById("modal-teacher-name").value.trim();
  if (!name) {
    showToast("Vui lòng nhập họ tên");
    return;
  }
  cls.teachers.push({ id: uid("teacher"), name });
  saveData();
  closeModal();
  render();
  showToast("Đã thêm Giáo lý viên");
}

function editTeacher(classId, teacherId) {
  const teacher = getTeacher(classId, teacherId);
  if (!teacher) return;
  showModal(`
    <div class="modal-title">Sửa Giáo lý viên</div>
    <div class="form-group">
      <label class="form-label">Họ tên (hiển thị trong quyết định)</label>
      <input class="form-input" id="modal-teacher-name" value="${esc(teacher.name)}" />
    </div>
    <button class="btn-secondary" data-action="close-modal">HỦY</button>
    <button class="btn-primary" style="margin-top:10px;" data-action="save-edit-teacher" data-class-id="${esc(classId)}" data-teacher-id="${esc(teacherId)}">LƯU THAY ĐỔI</button>
  `);
}

function saveEditTeacher(classId, teacherId) {
  const teacher = getTeacher(classId, teacherId);
  if (!teacher) return;
  const name = document.getElementById("modal-teacher-name").value.trim();
  if (!name) {
    showToast("Vui lòng nhập họ tên");
    return;
  }
  teacher.name = name;
  saveData();
  closeModal();
  render();
  showToast("Đã cập nhật Giáo lý viên");
}

function deleteTeacher(classId, teacherId) {
  const teacher = getTeacher(classId, teacherId);
  if (!teacher) return;
  openConfirmDialog({
    title: "Xóa Giáo lý viên",
    message: `Bạn có chắc muốn xóa Giáo lý viên "${teacher.name}"?`,
    confirmLabel: "XÓA",
    danger: true,
    onConfirm: () => {
      const cls = getClass(classId);
      cls.teachers = cls.teachers.filter((t) => t.id !== teacherId);
      saveData();
      closeModal();
      render();
      showToast("Đã xóa Giáo lý viên");
    }
  });
}

/* ==========================================================================
   ADMIN — IMPORT / EXPORT / RESET
   ========================================================================== */
function exportData() {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `glv-appointment-data-${data.schoolYear.replace(/\s/g, "")}.json`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
  showToast("Đã xuất dữ liệu");
}

function pickImportFile() {
  document.getElementById("import-file-input").click();
}

function handleImportFileChange(file) {
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    try {
      const parsed = JSON.parse(reader.result);
      if (!parsed || !Array.isArray(parsed.classes)) {
        throw new Error("invalid shape");
      }
      data = parsed;
      saveData();
      render();
      showToast("Đã nhập dữ liệu thành công");
    } catch (e) {
      showToast("File dữ liệu không hợp lệ");
    }
  };
  reader.readAsText(file);
}

function resetData() {
  openConfirmDialog({
    title: "Khôi phục dữ liệu mặc định",
    message: "Toàn bộ dữ liệu hiện tại (lớp, Giáo lý viên, thông tin chung, logo) sẽ bị xóa và thay bằng dữ liệu mặc định. Hành động này không thể hoàn tác.",
    confirmLabel: "KHÔI PHỤC",
    danger: true,
    onConfirm: () => {
      data = deepClone(defaultData);
      saveData();
      closeModal();
      adminEditingClassId = null;
      render();
      showToast("Đã khôi phục dữ liệu mặc định");
    }
  });
}

/* ==========================================================================
   EVENT DELEGATION
   ========================================================================== */
function handleGlobalClick(e) {
  const overlay = e.target.closest(".modal-overlay");
  if (overlay && e.target === overlay) {
    closeModal();
    return;
  }

  const btn = e.target.closest("[data-action]");
  if (!btn) return;

  const action = btn.dataset.action;
  const classId = btn.dataset.classId;
  const teacherId = btn.dataset.teacherId;

  switch (action) {
    case "go-home": showHome(); break;
    case "open-class": showClass(classId); break;
    case "back-to-class": showClass(classId); break;
    case "open-teacher": showTeacher(classId, teacherId); break;
    case "back-to-teacher": showTeacher(classId, teacherId); break;
    case "open-envelope": openEnvelope(); break;
    case "open-confirm-modal": openConfirmAppointmentModal(); break;
    case "confirm-appointment": confirmAppointment(); break;
    case "close-modal": closeModal(); break;
    case "run-confirm":
      if (typeof window.__pendingConfirm === "function") {
        const fn = window.__pendingConfirm;
        window.__pendingConfirm = null;
        fn();
      }
      break;

    case "admin-open": openAdmin(); break;
    case "login-admin": loginAdmin(); break;
    case "save-settings": saveSettings(); break;
    case "pick-logo-file": pickLogoFile(); break;
    case "reset-logo": resetLogo(); break;
    case "toggle-class-teachers": toggleClassTeachers(classId); break;
    case "add-class": addClass(); break;
    case "save-new-class": saveNewClass(); break;
    case "edit-class": editClass(classId); break;
    case "save-edit-class": saveEditClass(classId); break;
    case "delete-class": deleteClass(classId); break;
    case "add-teacher": addTeacher(classId); break;
    case "save-new-teacher": saveNewTeacher(classId); break;
    case "edit-teacher": editTeacher(classId, teacherId); break;
    case "save-edit-teacher": saveEditTeacher(classId, teacherId); break;
    case "delete-teacher": deleteTeacher(classId, teacherId); break;
    case "export-data": exportData(); break;
    case "pick-import-file": pickImportFile(); break;
    case "reset-data": resetData(); break;
    default: break;
  }
}

function handleGlobalChange(e) {
  if (e.target.id === "logo-file-input") {
    handleLogoFileChange(e.target.files[0]);
    e.target.value = "";
  }
  if (e.target.id === "import-file-input") {
    handleImportFileChange(e.target.files[0]);
    e.target.value = "";
  }
}

/* ==========================================================================
   INIT
   ========================================================================== */
function init() {
  loadData();
  setClassTheme(data.classes[0] ? data.classes[0].color : "#245A78");

  document.body.addEventListener("click", handleGlobalClick);
  document.body.addEventListener("change", handleGlobalChange);
  document.getElementById("admin-fab").addEventListener("click", openAdmin);

  render();
}

document.addEventListener("DOMContentLoaded", init);
