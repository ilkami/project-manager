# Project Manager — управление личными проектами программиста

Учебный проект: десктопное приложение на C#/WPF (.NET 8) и сайт на Python/Flask.
Учёт проектов, задач, сроков, технологий и прогресса. Авторизация без капчи, окно администратора.

## Состав

```
Desktop/                          WPF-приложение (.NET 8)
├── ProjectManager.sln            решение для Visual Studio
└── ProjectManager.Desktop/
    ├── LoginWindow.xaml          форма авторизации
    ├── MainWindow.xaml           проекты, задачи, технологии, статистика
    ├── AdminWindow.xaml          окно администратора
    ├── Dialogs/                  диалоги проекта, задачи, учётной записи
    ├── Models/                   User, Project, TaskItem, Technology
    ├── Data/                     SQLite: схема, репозитории
    └── Services/                 PBKDF2-хеширование, сессия, разбор дат

Web/                              сайт на Flask
├── app.py                        маршруты и работа с БД
├── templates/                    10 страниц (Jinja2)
├── static/css/style.css          тёмная тема
├── requirements.txt
├── wsgi.py                       точка входа для PythonAnywhere
└── run.bat                       запуск в один клик (Windows)
```

## Демо-доступ

| Логин    | Пароль    | Роль           |
|----------|-----------|----------------|
| admin    | admin123  | администратор  |
| ivanov   | user123   | пользователь   |
| petrova  | user123   | пользователь   |

## Запуск сайта

Нужен Python 3.11+ (проверено на 3.14).

Способ 1 — двойной щелчок по `Web\run.bat`.

Способ 2 — вручную:

```bat
cd Web
pip install -r requirements.txt
python app.py
```

Открыть http://127.0.0.1:5000

База создаётся автоматически: `Web/instance/project_manager.db` (при первом запуске заполняется демо-данными).

## Запуск десктопа

Нужна Visual Studio 2022/2026 с рабочей нагрузкой «Разработка классических приложений .NET» (она ставит .NET 8 SDK).

Открыть `Desktop\ProjectManager.sln`, нажать F5. Либо из консоли:

```bat
cd Desktop\ProjectManager.Desktop
dotnet run
```

База: `%AppData%\ProjectManager\projects.db` (создаётся при первом запуске).

## Публикация на PythonAnywhere

Бесплатный аккаунт: один сайт с адресом `USERNAME.pythonanywhere.com`.
SQLite-файл хранится постоянно — данные не пропадают при перезапуске.

### Шаг 1. Загрузить проект

Вкладка **Files** → *Upload a file* → выбрать `ProjectManager.zip`.
Затем вкладка **Consoles** → *Bash*:

```bash
cd ~
unzip -o ProjectManager.zip -d upload
mv ~/upload/Web ~/mysite
```

Если папка `mysite` уже существует и пуста — сначала `rmdir ~/mysite`.

### Шаг 2. Зависимости

```bash
cd ~/mysite
mkvirtualenv --python=/usr/bin/python3.10 venv
pip install -r requirements.txt
```

Если Python 3.10 недоступен: `ls /usr/bin/python3*` и подставить существующую версию (3.9+).

### Шаг 3. Создать веб-приложение

Вкладка **Web** → **Add a new web app** → **Manual configuration** → Python 3.10.

- **Source code:** `/home/USERNAME/mysite`
- **Working directory:** `/home/USERNAME/mysite`
- **Virtualenv:** `/home/USERNAME/.virtualenvs/venv`

### Шаг 4. WSGI-файл

На вкладке Web — ссылка **WSGI configuration file**. Стереть всё содержимое и вставить:

```python
import os, sys

path = '/home/USERNAME/mysite'
if path not in sys.path:
    sys.path.insert(0, path)

os.environ['SECRET_KEY'] = 'любая-длинная-случайная-строка'

from app import app as application
```

`USERNAME` заменить на свой логин. Сохранить файл.

### Шаг 5. Перезапуск

Зелёная кнопка **Reload** на вкладке Web. Адрес сайта: `https://USERNAME.pythonanywhere.com`.
Вход: `admin` / `admin123`.

### Если не работает

Вкладка Web → ссылка **error log** (`/var/log/USERNAME.pythonanywhere.com.error.log`).
Частые причины:

| В логе | Причина | Что делать |
|---|---|---|
| `No module named 'flask'` | зависимости поставлены не в тот virtualenv | повторить `pip install -r requirements.txt` после `workon venv` |
| `unable to open database file` | нет папки или прав | `mkdir -p ~/mysite/instance` |
| страница «Welcome to Flask» | WSGI-файл не сохранён | вставить блок из шага 4 и нажать Reload |
| `Internal Server Error` | любая ошибка приложения | смотреть первые строки лога — там traceback |

### Альтернативы, если PythonAnywhere недоступен

- **Render** — бесплатно, из GitHub; `Build: pip install -r requirements-deploy.txt`, `Start: gunicorn app:app`, переменная `SECRET_KEY`. Минусы: засыпает через 15 минут, база сбрасывается при передеплое.
- **Docker** — `docker build -t project-manager . && docker run -p 7860:7860 project-manager` (для Hugging Face Spaces, VPS).
- **Beget / Timeweb** — платно (~200–300 ₽/мес), но без ограничений доступа.

## Особенности реализации

- Пароли хранятся как PBKDF2-SHA256, 100 000 итераций, соль 16 байт (обе части проекта).
- Прогресс проекта не вводится вручную — считается как доля выполненных задач.
- Десктоп и сайт используют **разные** базы (локальный SQLite и SQLite на хостинге),
  поэтому данные между ними не синхронизируются. Обмен через API — возможное развитие проекта.
- Капчи нет по требованию задания; защита — проверка логина/пароля и блокировка учётной записи администратором.
