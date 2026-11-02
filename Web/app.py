"""
Веб-приложение для управления личными проектами программиста.

Стек: Python + Flask + SQLite.
Возможности: авторизация без капчи, проекты, задачи, сроки, технологии, прогресс,
панель администратора (управление учётными записями).

Запуск локально:  python app.py  ->  http://127.0.0.1:5000
Демо-доступ:      admin / admin123 (администратор), ivanov / user123 (пользователь)
"""

import os
import sqlite3
from datetime import date, datetime, timedelta
from functools import wraps

from flask import (Flask, flash, g, redirect, render_template, request,
                   session, url_for)
from werkzeug.security import check_password_hash, generate_password_hash

BASE_DIR = os.path.abspath(os.path.dirname(__file__))
DB_PATH = os.environ.get("PM_DB_PATH", os.path.join(BASE_DIR, "instance", "project_manager.db"))

app = Flask(__name__)
app.config["SECRET_KEY"] = os.environ.get("SECRET_KEY", "project-manager-demo-secret")
app.config["DATABASE"] = DB_PATH

STATUS_NAMES = {
    "plan": "Планирование",
    "work": "В работе",
    "test": "Тестирование",
    "done": "Завершён",
}

PRIORITY_NAMES = {
    "low": "Низкий",
    "normal": "Обычный",
    "high": "Высокий",
}

ROLE_NAMES = {
    "user": "Пользователь",
    "admin": "Администратор",
}

SCHEMA = """
CREATE TABLE IF NOT EXISTS users (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    login         TEXT    NOT NULL UNIQUE,
    password_hash TEXT    NOT NULL,
    full_name     TEXT    NOT NULL DEFAULT '',
    role          TEXT    NOT NULL DEFAULT 'user',
    is_active     INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS technologies (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS projects (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id     INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title       TEXT    NOT NULL,
    description TEXT    NOT NULL DEFAULT '',
    start_date  TEXT,
    deadline    TEXT,
    status      TEXT    NOT NULL DEFAULT 'work',
    repo_url    TEXT    NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS project_technologies (
    project_id    INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    technology_id INTEGER NOT NULL REFERENCES technologies(id) ON DELETE CASCADE,
    PRIMARY KEY (project_id, technology_id)
);

CREATE TABLE IF NOT EXISTS tasks (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    project_id  INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    title       TEXT    NOT NULL,
    description TEXT    NOT NULL DEFAULT '',
    priority    TEXT    NOT NULL DEFAULT 'normal',
    due_date    TEXT,
    is_done     INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_tasks_project ON tasks(project_id);
CREATE INDEX IF NOT EXISTS ix_projects_user ON projects(user_id);
"""


# --------------------------------------------------------------------- база данных

def get_db():
    if "db" not in g:
        os.makedirs(os.path.dirname(app.config["DATABASE"]) or ".", exist_ok=True)
        g.db = sqlite3.connect(app.config["DATABASE"])
        g.db.row_factory = sqlite3.Row
        g.db.execute("PRAGMA foreign_keys = ON")
    return g.db


@app.teardown_appcontext
def close_db(exception=None):
    db = g.pop("db", None)
    if db is not None:
        db.close()


def init_db():
    """Создаёт таблицы и заполняет базу демонстрационными данными при первом запуске."""
    db = get_db()
    db.executescript(SCHEMA)
    db.commit()

    if db.execute("SELECT COUNT(*) FROM users").fetchone()[0] > 0:
        return

    def add_user(login, password, full_name, role):
        cur = db.execute(
            "INSERT INTO users (login, password_hash, full_name, role, is_active, created_at) "
            "VALUES (?, ?, ?, ?, 1, ?)",
            (login, generate_password_hash(password), full_name, role, date.today().isoformat()),
        )
        return cur.lastrowid

    admin_id = add_user("admin", "admin123", "Администратор системы", "admin")
    ivanov_id = add_user("ivanov", "user123", "Иван Иванов", "user")
    petrova_id = add_user("petrova", "user123", "Анна Петрова", "user")

    technologies = ["C#", ".NET 8", "WPF", "Python", "Flask", "SQLite",
                    "HTML/CSS", "JavaScript", "Git", "REST API", "PostgreSQL", "Docker"]
    tech_ids = {}
    for name in technologies:
        tech_ids[name] = db.execute("INSERT INTO technologies (name) VALUES (?)", (name,)).lastrowid

    today = date.today()

    def add_project(user_id, title, description, start, deadline, status, repo, techs, tasks):
        project_id = db.execute(
            "INSERT INTO projects (user_id, title, description, start_date, deadline, status, repo_url) "
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            (user_id, title, description, start, deadline, status, repo),
        ).lastrowid

        for tech in techs:
            db.execute(
                "INSERT OR IGNORE INTO project_technologies (project_id, technology_id) VALUES (?, ?)",
                (project_id, tech_ids[tech]),
            )

        for task in tasks:
            db.execute(
                "INSERT INTO tasks (project_id, title, description, priority, due_date, is_done, created_at) "
                "VALUES (?, ?, '', ?, ?, ?, ?)",
                (project_id, task[0], task[1], (today + timedelta(days=task[2])).isoformat(),
                 1 if task[3] else 0, today.isoformat()),
            )

    add_project(
        ivanov_id,
        "Веб-приложение для управления личными проектами",
        "Учебный проект: WPF-клиент на C# и сайт на Flask для учёта проектов, задач, сроков и технологий.",
        (today - timedelta(days=20)).isoformat(), (today + timedelta(days=12)).isoformat(), "work",
        "https://github.com/ivanov/project-manager",
        ["C#", ".NET 8", "WPF", "Python", "Flask", "SQLite"],
        [("Спроектировать схему базы данных", "normal", -14, True),
         ("Реализовать форму авторизации", "high", -10, True),
         ("Сделать окно администратора", "high", -4, True),
         ("Добавить расчёт прогресса по задачам", "normal", -2, False),
         ("Написать сайт на Flask", "high", 5, False),
         ("Задеплоить сайт на PythonAnywhere", "high", 9, False)],
    )

    add_project(
        ivanov_id,
        "Парсер вакансий",
        "Сбор вакансий с публичного API, выгрузка в SQLite и отчёт по требуемым технологиям.",
        (today - timedelta(days=30)).isoformat(), (today + timedelta(days=5)).isoformat(), "test",
        "https://github.com/ivanov/vacancy-parser",
        ["Python", "REST API", "SQLite", "Git"],
        [("Изучить документацию API", "normal", -25, True),
         ("Реализовать сбор вакансий", "high", -18, True),
         ("Добавить сохранение в базу", "normal", -12, True),
         ("Написать отчёт по технологиям", "normal", 3, False),
         ("Проверить обработку ошибок сети", "low", 4, False)],
    )

    add_project(
        ivanov_id,
        "Трекер привычек",
        "Личное приложение для отметок о привычках и подсчёта серий.",
        (today - timedelta(days=3)).isoformat(), (today + timedelta(days=45)).isoformat(), "plan",
        "", ["C#", ".NET 8", "WPF"],
        [("Собрать требования", "normal", 7, False),
         ("Нарисовать макеты экранов", "low", 14, False),
         ("Сверстать главное окно", "normal", 30, False)],
    )

    add_project(
        ivanov_id,
        "Лендинг портфолио",
        "Одностраничный сайт с описанием навыков и ссылками на проекты.",
        (today - timedelta(days=60)).isoformat(), (today - timedelta(days=10)).isoformat(), "done",
        "https://github.com/ivanov/portfolio",
        ["HTML/CSS", "JavaScript", "Git"],
        [("Свёрстать секции", "normal", -45, True),
         ("Адаптивность под мобильные", "low", -30, True),
         ("Опубликовать на хостинге", "normal", -12, True)],
    )

    add_project(
        petrova_id,
        "Telegram-бот для заметок",
        "Бот принимает текст и складывает заметки в базу, поддерживает поиск по словам.",
        (today - timedelta(days=7)).isoformat(), (today + timedelta(days=20)).isoformat(), "work",
        "https://github.com/petrova/notes-bot",
        ["Python", "REST API", "PostgreSQL"],
        [("Зарегистрировать бота", "high", -5, True),
         ("Реализовать сохранение заметок", "high", 2, False),
         ("Добавить поиск", "normal", 10, False),
         ("Настроить деплой", "low", 18, False)],
    )

    db.commit()


# --------------------------------------------------------------------- утилиты

def parse_date(text):
    """'дд.мм.гггг' или 'гггг-мм-дд' -> date. Пустая строка -> None."""
    text = (text or "").strip()
    if not text:
        return None
    for fmt in ("%d.%m.%Y", "%Y-%m-%d", "%d.%m.%y"):
        try:
            return datetime.strptime(text, fmt).date()
        except ValueError:
            continue
    return None


def ru_date(value):
    if not value:
        return "—"
    if isinstance(value, str):
        try:
            value = datetime.strptime(value, "%Y-%m-%d").date()
        except ValueError:
            return value
    return value.strftime("%d.%m.%Y")


def days_left_text(deadline, status):
    if not deadline:
        return "—"
    if status == "done":
        return "завершён"
    days = (parse_date(deadline) - date.today()).days
    if days < 0:
        return f"просрочен на {-days} дн."
    if days == 0:
        return "сегодня"
    return f"{days} дн."


def is_overdue(deadline, status):
    return bool(deadline) and status != "done" and parse_date(deadline) < date.today()


def progress_of(total, done):
    if not total:
        return 0
    return int(round(done * 100 / total))


def enrich_project(row):
    project = dict(row)
    project["progress"] = progress_of(project["tasks_total"], project["tasks_done"])
    project["status_text"] = STATUS_NAMES.get(project["status"], project["status"])
    project["deadline_text"] = ru_date(project["deadline"])
    project["start_text"] = ru_date(project["start_date"])
    project["days_left_text"] = days_left_text(project["deadline"], project["status"])
    project["overdue"] = is_overdue(project["deadline"], project["status"])
    project["technologies_text"] = project["technologies"] or "—"
    return project


def enrich_task(row):
    task = dict(row)
    task["priority_text"] = PRIORITY_NAMES.get(task["priority"], task["priority"])
    task["due_text"] = ru_date(task["due_date"])
    task["status_text"] = "Выполнена" if task["is_done"] else "Открыта"
    task["overdue"] = bool(task["due_date"]) and not task["is_done"] and parse_date(task["due_date"]) < date.today()
    return task


PROJECT_SELECT = """
SELECT p.*, u.login AS owner_login,
       (SELECT COUNT(*) FROM tasks t WHERE t.project_id = p.id) AS tasks_total,
       (SELECT COUNT(*) FROM tasks t WHERE t.project_id = p.id AND t.is_done = 1) AS tasks_done,
       (SELECT GROUP_CONCAT(tc.name, ', ')
          FROM project_technologies pt
          JOIN technologies tc ON tc.id = pt.technology_id
         WHERE pt.project_id = p.id) AS technologies
FROM projects p
JOIN users u ON u.id = p.user_id
"""


def load_projects(project_id=None):
    sql = PROJECT_SELECT
    params = []

    if g.user["role"] != "admin":
        sql += " WHERE p.user_id = ?"
        params.append(g.user["id"])

    if project_id is not None:
        sql += " AND p.id = ?" if params else " WHERE p.id = ?"
        params.append(project_id)

    sql += (" ORDER BY CASE p.status WHEN 'done' THEN 1 ELSE 0 END,"
            " p.deadline IS NULL, p.deadline, p.id")

    rows = get_db().execute(sql, params).fetchall()
    return [enrich_project(r) for r in rows]


def load_tasks(project_id=None, only_open=False):
    sql = """
    SELECT t.*, p.title AS project_title, p.user_id AS project_owner
    FROM tasks t
    JOIN projects p ON p.id = t.project_id
    """
    conditions = []
    params = []

    if g.user["role"] != "admin":
        conditions.append("p.user_id = ?")
        params.append(g.user["id"])

    if project_id:
        conditions.append("t.project_id = ?")
        params.append(project_id)

    if only_open:
        conditions.append("t.is_done = 0")

    if conditions:
        sql += " WHERE " + " AND ".join(conditions)

    sql += (" ORDER BY t.is_done,"
            " CASE t.priority WHEN 'high' THEN 0 WHEN 'normal' THEN 1 ELSE 2 END,"
            " t.due_date IS NULL, t.due_date, t.id")

    return [enrich_task(r) for r in get_db().execute(sql, params).fetchall()]


def get_project(project_id):
    projects = load_projects(project_id)
    return projects[0] if projects else None


def sync_technologies(project_id, raw_text):
    """Привязывает технологии по строке 'C#, Python'. Новые названия добавляет в справочник."""
    db = get_db()
    db.execute("DELETE FROM project_technologies WHERE project_id = ?", (project_id,))

    for name in [x.strip() for x in (raw_text or "").replace(";", ",").split(",")]:
        if not name:
            continue
        row = db.execute("SELECT id FROM technologies WHERE name = ? COLLATE NOCASE", (name,)).fetchone()
        tech_id = row["id"] if row else db.execute(
            "INSERT INTO technologies (name) VALUES (?)", (name,)).lastrowid
        db.execute(
            "INSERT OR IGNORE INTO project_technologies (project_id, technology_id) VALUES (?, ?)",
            (project_id, tech_id),
        )


def login_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if g.user is None:
            return redirect(url_for("login", next=request.path))
        return view(*args, **kwargs)

    return wrapped


def admin_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if g.user is None:
            return redirect(url_for("login"))
        if g.user["role"] != "admin":
            flash("Раздел доступен только администратору.", "error")
            return redirect(url_for("dashboard"))
        return view(*args, **kwargs)

    return wrapped


@app.before_request
def load_user():
    user_id = session.get("user_id")
    g.user = None
    if user_id:
        g.user = get_db().execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
        if g.user is not None and not g.user["is_active"]:
            session.clear()
            g.user = None


@app.context_processor
def inject_globals():
    return {
        "current_user": g.get("user"),
        "status_names": STATUS_NAMES,
        "priority_names": PRIORITY_NAMES,
        "role_names": ROLE_NAMES,
    }


# --------------------------------------------------------------------- авторизация

@app.route("/login", methods=["GET", "POST"])
def login():
    if g.user:
        return redirect(url_for("dashboard"))

    error = None

    if request.method == "POST":
        login_value = request.form.get("login", "").strip()
        password = request.form.get("password", "")

        user = get_db().execute(
            "SELECT * FROM users WHERE login = ? COLLATE NOCASE", (login_value,)).fetchone()

        if user is None:
            error = "Пользователь с таким логином не найден."
        elif not check_password_hash(user["password_hash"], password):
            error = "Неверный пароль."
        elif not user["is_active"]:
            error = "Учётная запись заблокирована администратором."
        else:
            session.clear()
            session["user_id"] = user["id"]
            return redirect(request.args.get("next") or url_for("dashboard"))

    return render_template("login.html", error=error)


@app.route("/logout")
def logout():
    session.clear()
    flash("Вы вышли из системы.", "info")
    return redirect(url_for("login"))


# --------------------------------------------------------------------- дашборд

@app.route("/")
@login_required
def dashboard():
    projects = load_projects()
    tasks = load_tasks()

    work = [p for p in projects if p["status"] in ("work", "test")]
    overdue = [p for p in projects if p["overdue"]]
    done_tasks = [t for t in tasks if t["is_done"]]
    open_tasks = [t for t in tasks if not t["is_done"]]

    upcoming = sorted(
        [t for t in open_tasks if t["due_date"]],
        key=lambda t: t["due_date"],
    )[:6]

    average = int(round(sum(p["progress"] for p in projects) / len(projects))) if projects else 0

    return render_template(
        "dashboard.html",
        projects=projects,
        upcoming=upcoming,
        open_count=len(open_tasks),
        done_count=len(done_tasks),
        stats={
            "projects": len(projects),
            "work": len(work),
            "overdue": len(overdue),
            "tasks": len(tasks),
            "done": len(done_tasks),
            "average": average,
        },
    )


# --------------------------------------------------------------------- проекты

@app.route("/projects")
@login_required
def projects():
    return render_template("projects.html", projects=load_projects())


@app.route("/projects/new", methods=["GET", "POST"])
@login_required
def project_create():
    if request.method == "POST":
        title = request.form.get("title", "").strip()
        start_date = parse_date(request.form.get("start_date"))
        deadline = parse_date(request.form.get("deadline"))

        if not title:
            flash("Укажите название проекта.", "error")
        elif request.form.get("start_date") and start_date is None:
            flash("Дата начала должна быть в формате дд.мм.гггг.", "error")
        elif request.form.get("deadline") and deadline is None:
            flash("Срок сдачи должен быть в формате дд.мм.гггг.", "error")
        elif start_date and deadline and deadline < start_date:
            flash("Срок сдачи не может быть раньше даты начала.", "error")
        else:
            db = get_db()
            project_id = db.execute(
                "INSERT INTO projects (user_id, title, description, start_date, deadline, status, repo_url) "
                "VALUES (?, ?, ?, ?, ?, ?, ?)",
                (g.user["id"], title, request.form.get("description", "").strip(),
                 start_date.isoformat() if start_date else None,
                 deadline.isoformat() if deadline else None,
                 request.form.get("status", "work"),
                 request.form.get("repo_url", "").strip()),
            ).lastrowid
            sync_technologies(project_id, request.form.get("technologies"))
            db.commit()
            flash("Проект создан.", "success")
            return redirect(url_for("project_detail", project_id=project_id))

    return render_template("project_form.html", project=None, form=request.form)


@app.route("/projects/<int:project_id>/edit", methods=["GET", "POST"])
@login_required
def project_edit(project_id):
    project = get_project(project_id)
    if project is None:
        flash("Проект не найден.", "error")
        return redirect(url_for("projects"))

    if request.method == "POST":
        title = request.form.get("title", "").strip()
        start_date = parse_date(request.form.get("start_date"))
        deadline = parse_date(request.form.get("deadline"))

        if not title:
            flash("Укажите название проекта.", "error")
        elif request.form.get("start_date") and start_date is None:
            flash("Дата начала должна быть в формате дд.мм.гггг.", "error")
        elif request.form.get("deadline") and deadline is None:
            flash("Срок сдачи должен быть в формате дд.мм.гггг.", "error")
        elif start_date and deadline and deadline < start_date:
            flash("Срок сдачи не может быть раньше даты начала.", "error")
        else:
            db = get_db()
            db.execute(
                "UPDATE projects SET title = ?, description = ?, start_date = ?, deadline = ?, "
                "status = ?, repo_url = ? WHERE id = ?",
                (title, request.form.get("description", "").strip(),
                 start_date.isoformat() if start_date else None,
                 deadline.isoformat() if deadline else None,
                 request.form.get("status", "work"),
                 request.form.get("repo_url", "").strip(),
                 project_id),
            )
            sync_technologies(project_id, request.form.get("technologies"))
            db.commit()
            flash("Изменения сохранены.", "success")
            return redirect(url_for("project_detail", project_id=project_id))

    if not request.form:
        form = {
            "title": project["title"],
            "description": project["description"],
            "start_date": ru_date(project["start_date"]) if project["start_date"] else "",
            "deadline": ru_date(project["deadline"]) if project["deadline"] else "",
            "status": project["status"],
            "repo_url": project["repo_url"],
            "technologies": project["technologies"] or "",
        }
        return render_template("project_form.html", project=project, form=form)

    return render_template("project_form.html", project=project, form=request.form)


@app.route("/projects/<int:project_id>")
@login_required
def project_detail(project_id):
    project = get_project(project_id)
    if project is None:
        flash("Проект не найден.", "error")
        return redirect(url_for("projects"))

    return render_template("project_detail.html",
                           project=project,
                           tasks=load_tasks(project_id=project_id))


@app.route("/projects/<int:project_id>/delete", methods=["POST"])
@login_required
def project_delete(project_id):
    project = get_project(project_id)
    if project is None:
        flash("Проект не найден.", "error")
        return redirect(url_for("projects"))

    db = get_db()
    db.execute("DELETE FROM tasks WHERE project_id = ?", (project_id,))
    db.execute("DELETE FROM project_technologies WHERE project_id = ?", (project_id,))
    db.execute("DELETE FROM projects WHERE id = ?", (project_id,))
    db.commit()

    flash("Проект удалён.", "success")
    return redirect(url_for("projects"))


# --------------------------------------------------------------------- задачи

@app.route("/tasks")
@login_required
def tasks():
    project_id = request.args.get("project", type=int)
    only_open = request.args.get("open") == "1"

    return render_template(
        "tasks.html",
        tasks=load_tasks(project_id=project_id, only_open=only_open),
        projects=load_projects(),
        selected_project=project_id,
        only_open=only_open,
    )


@app.route("/projects/<int:project_id>/tasks", methods=["POST"])
@login_required
def task_create(project_id):
    project = get_project(project_id)
    if project is None:
        flash("Проект не найден.", "error")
        return redirect(url_for("projects"))

    title = request.form.get("title", "").strip()
    due = parse_date(request.form.get("due_date"))

    if not title:
        flash("Укажите название задачи.", "error")
    elif request.form.get("due_date") and due is None:
        flash("Срок должен быть в формате дд.мм.гггг.", "error")
    else:
        db = get_db()
        db.execute(
            "INSERT INTO tasks (project_id, title, description, priority, due_date, is_done, created_at) "
            "VALUES (?, ?, ?, ?, ?, 0, ?)",
            (project_id, title, request.form.get("description", "").strip(),
             request.form.get("priority", "normal"),
             due.isoformat() if due else None,
             date.today().isoformat()),
        )
        db.commit()
        flash("Задача добавлена.", "success")

    return redirect(url_for("project_detail", project_id=project_id))


@app.route("/tasks/<int:task_id>/edit", methods=["GET", "POST"])
@login_required
def task_edit(task_id):
    row = get_db().execute(
        "SELECT t.*, p.title AS project_title, p.user_id AS project_owner "
        "FROM tasks t JOIN projects p ON p.id = t.project_id WHERE t.id = ?",
        (task_id,)).fetchone()

    if row is None or (g.user["role"] != "admin" and row["project_owner"] != g.user["id"]):
        flash("Задача не найдена.", "error")
        return redirect(url_for("tasks"))

    task = enrich_task(row)

    if request.method == "POST":
        title = request.form.get("title", "").strip()
        due = parse_date(request.form.get("due_date"))

        if not title:
            flash("Укажите название задачи.", "error")
        elif request.form.get("due_date") and due is None:
            flash("Срок должен быть в формате дд.мм.гггг.", "error")
        else:
            db = get_db()
            db.execute(
                "UPDATE tasks SET title = ?, description = ?, priority = ?, due_date = ?, is_done = ? "
                "WHERE id = ?",
                (title, request.form.get("description", "").strip(),
                 request.form.get("priority", "normal"),
                 due.isoformat() if due else None,
                 1 if request.form.get("is_done") else 0,
                 task_id),
            )
            db.commit()
            flash("Задача сохранена.", "success")
            return redirect(url_for("project_detail", project_id=task["project_id"]))

    return render_template("task_form.html", task=task, form=request.form)


@app.route("/tasks/<int:task_id>/toggle", methods=["POST"])
@login_required
def task_toggle(task_id):
    row = get_db().execute(
        "SELECT t.*, p.user_id AS project_owner FROM tasks t "
        "JOIN projects p ON p.id = t.project_id WHERE t.id = ?", (task_id,)).fetchone()

    if row is None or (g.user["role"] != "admin" and row["project_owner"] != g.user["id"]):
        flash("Задача не найдена.", "error")
        return redirect(url_for("tasks"))

    db = get_db()
    db.execute("UPDATE tasks SET is_done = ? WHERE id = ?", (0 if row["is_done"] else 1, task_id))
    db.commit()

    return redirect(request.form.get("back") or url_for("project_detail", project_id=row["project_id"]))


@app.route("/tasks/<int:task_id>/delete", methods=["POST"])
@login_required
def task_delete(task_id):
    row = get_db().execute(
        "SELECT t.*, p.user_id AS project_owner FROM tasks t "
        "JOIN projects p ON p.id = t.project_id WHERE t.id = ?", (task_id,)).fetchone()

    if row is None or (g.user["role"] != "admin" and row["project_owner"] != g.user["id"]):
        flash("Задача не найдена.", "error")
        return redirect(url_for("tasks"))

    db = get_db()
    db.execute("DELETE FROM tasks WHERE id = ?", (task_id,))
    db.commit()
    flash("Задача удалена.", "success")

    return redirect(request.form.get("back") or url_for("project_detail", project_id=row["project_id"]))


# --------------------------------------------------------------------- технологии

@app.route("/technologies")
@login_required
def technologies():
    rows = get_db().execute(
        "SELECT t.id, t.name, "
        "(SELECT COUNT(*) FROM project_technologies pt WHERE pt.technology_id = t.id) AS projects_count "
        "FROM technologies t ORDER BY t.name COLLATE NOCASE").fetchall()
    return render_template("technologies.html", technologies=rows)


@app.route("/technologies/new", methods=["POST"])
@login_required
def technology_create():
    name = request.form.get("name", "").strip()
    db = get_db()

    if not name:
        flash("Название технологии не может быть пустым.", "error")
    elif db.execute("SELECT 1 FROM technologies WHERE name = ? COLLATE NOCASE", (name,)).fetchone():
        flash("Такая технология уже есть в справочнике.", "error")
    else:
        db.execute("INSERT INTO technologies (name) VALUES (?)", (name,))
        db.commit()
        flash("Технология добавлена.", "success")

    return redirect(url_for("technologies"))


@app.route("/technologies/<int:tech_id>/delete", methods=["POST"])
@login_required
def technology_delete(tech_id):
    db = get_db()
    used = db.execute("SELECT COUNT(*) FROM project_technologies WHERE technology_id = ?",
                      (tech_id,)).fetchone()[0]

    if used:
        flash("Технология используется в проектах, сначала уберите её из проектов.", "error")
    else:
        db.execute("DELETE FROM technologies WHERE id = ?", (tech_id,))
        db.commit()
        flash("Технология удалена.", "success")

    return redirect(url_for("technologies"))


# --------------------------------------------------------------------- администрирование

@app.route("/admin/users")
@admin_required
def admin_users():
    users = get_db().execute(
        "SELECT u.*, (SELECT COUNT(*) FROM projects p WHERE p.user_id = u.id) AS projects_count "
        "FROM users u ORDER BY u.id").fetchall()

    stats = {
        "total": len(users),
        "admins": sum(1 for u in users if u["role"] == "admin"),
        "blocked": sum(1 for u in users if not u["is_active"]),
        "projects": sum(u["projects_count"] for u in users),
    }

    return render_template("admin_users.html", users=users, stats=stats)


@app.route("/admin/users/new", methods=["GET", "POST"])
@admin_required
def admin_user_create():
    if request.method == "POST":
        login_value = request.form.get("login", "").strip()
        password = request.form.get("password", "")
        role = "admin" if request.form.get("role") == "admin" else "user"
        db = get_db()

        if not login_value:
            flash("Укажите логин.", "error")
        elif len(password) < 5:
            flash("Пароль должен содержать минимум 5 символов.", "error")
        elif db.execute("SELECT 1 FROM users WHERE login = ? COLLATE NOCASE", (login_value,)).fetchone():
            flash("Пользователь с таким логином уже существует.", "error")
        else:
            db.execute(
                "INSERT INTO users (login, password_hash, full_name, role, is_active, created_at) "
                "VALUES (?, ?, ?, ?, 1, ?)",
                (login_value, generate_password_hash(password),
                 request.form.get("full_name", "").strip(), role, date.today().isoformat()),
            )
            db.commit()
            flash("Учётная запись создана.", "success")
            return redirect(url_for("admin_users"))

    return render_template("user_form.html", user=None, form=request.form)


@app.route("/admin/users/<int:user_id>/edit", methods=["GET", "POST"])
@admin_required
def admin_user_edit(user_id):
    user = get_db().execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
    if user is None:
        flash("Пользователь не найден.", "error")
        return redirect(url_for("admin_users"))

    if request.method == "POST":
        login_value = request.form.get("login", "").strip()
        role = "admin" if request.form.get("role") == "admin" else "user"
        is_active = 1 if request.form.get("is_active") else 0
        new_password = request.form.get("password", "")
        db = get_db()

        duplicate = db.execute(
            "SELECT id FROM users WHERE login = ? COLLATE NOCASE AND id <> ?",
            (login_value, user_id)).fetchone()

        if not login_value:
            flash("Укажите логин.", "error")
        elif duplicate:
            flash("Этот логин уже занят другим пользователем.", "error")
        elif new_password and len(new_password) < 5:
            flash("Пароль должен содержать минимум 5 символов.", "error")
        elif role != "admin" and user["role"] == "admin" and last_admin(user_id):
            flash("Нельзя снять права с последнего администратора.", "error")
        elif not is_active and user_id == g.user["id"]:
            flash("Нельзя заблокировать собственную учётную запись.", "error")
        else:
            db.execute(
                "UPDATE users SET login = ?, full_name = ?, role = ?, is_active = ? WHERE id = ?",
                (login_value, request.form.get("full_name", "").strip(), role, is_active, user_id),
            )
            if new_password:
                db.execute("UPDATE users SET password_hash = ? WHERE id = ?",
                           (generate_password_hash(new_password), user_id))
            db.commit()
            flash("Изменения сохранены.", "success")
            return redirect(url_for("admin_users"))

    return render_template("user_form.html", user=user, form=request.form)


@app.route("/admin/users/<int:user_id>/toggle", methods=["POST"])
@admin_required
def admin_user_toggle(user_id):
    db = get_db()
    user = db.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()

    if user is None:
        flash("Пользователь не найден.", "error")
    elif user_id == g.user["id"]:
        flash("Нельзя заблокировать собственную учётную запись.", "error")
    elif user["is_active"] and user["role"] == "admin" and last_admin(user_id):
        flash("Нельзя заблокировать последнего администратора.", "error")
    else:
        db.execute("UPDATE users SET is_active = ? WHERE id = ?",
                   (0 if user["is_active"] else 1, user_id))
        db.commit()
        flash("Статус учётной записи изменён.", "success")

    return redirect(url_for("admin_users"))


@app.route("/admin/users/<int:user_id>/delete", methods=["POST"])
@admin_required
def admin_user_delete(user_id):
    db = get_db()

    if user_id == g.user["id"]:
        flash("Нельзя удалить собственную учётную запись.", "error")
        return redirect(url_for("admin_users"))

    if last_admin(user_id):
        flash("Нельзя удалить последнего администратора.", "error")
        return redirect(url_for("admin_users"))

    db.execute("DELETE FROM tasks WHERE project_id IN (SELECT id FROM projects WHERE user_id = ?)",
               (user_id,))
    db.execute("DELETE FROM project_technologies WHERE project_id IN "
               "(SELECT id FROM projects WHERE user_id = ?)", (user_id,))
    db.execute("DELETE FROM projects WHERE user_id = ?", (user_id,))
    db.execute("DELETE FROM users WHERE id = ?", (user_id,))
    db.commit()

    flash("Учётная запись удалена.", "success")
    return redirect(url_for("admin_users"))


def last_admin(user_id):
    db = get_db()
    admins = db.execute(
        "SELECT COUNT(*) FROM users WHERE role = 'admin' AND is_active = 1").fetchone()[0]
    is_admin = db.execute(
        "SELECT 1 FROM users WHERE id = ? AND role = 'admin'", (user_id,)).fetchone()
    return admins <= 1 and bool(is_admin)


# --------------------------------------------------------------------- запуск

with app.app_context():
    init_db()


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000, debug=True)
