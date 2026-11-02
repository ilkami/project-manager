"""
Точка входа для PythonAnywhere (WSGI).

Порядок публикации — подробно в README, раздел «Публикация на PythonAnywhere».
Кратко:

1. Загрузить содержимое папки Web в /home/USERNAME/mysite
   (проще всего: загрузить ProjectManager.zip через вкладку Files и распаковать в Bash-консоли).
2. В Bash-консоли:
       cd ~/mysite
       mkvirtualenv --python=/usr/bin/python3.10 venv
       pip install -r requirements.txt
3. Вкладка Web -> Add a new web app -> Manual configuration -> Python 3.10.
   - Source code:   /home/USERNAME/mysite
   - Working directory: /home/USERNAME/mysite
   - Virtualenv:    /home/USERNAME/.virtualenvs/venv
4. Открыть ссылку «WSGI configuration file» и заменить ВСЁ содержимое на блок ниже,
   подставив свой логин вместо USERNAME:

       import os, sys
       path = '/home/USERNAME/mysite'
       if path not in sys.path:
           sys.path.insert(0, path)
       os.environ['SECRET_KEY'] = 'любая-длинная-случайная-строка'
       from app import app as application

5. Нажать Reload. Адрес сайта: https://USERNAME.pythonanywhere.com

Если что-то не работает — смотреть лог:
/home/USERNAME/.pythonanywhere/../var/log/USERNAME.pythonanywhere.com.error.log
"""

import os
import sys

# Папка, где лежит этот файл и app.py (работает и локально, и на хостинге).
path = os.path.dirname(os.path.abspath(__file__))
if path not in sys.path:
    sys.path.insert(0, path)

# Секретный ключ сессий: на хостинге обязательно задайте своё значение
# (в WSGI-файле или в переменных окружения веб-приложения).
os.environ.setdefault("SECRET_KEY", "project-manager-please-change-this-key")

from app import app as application  # noqa: E402

application.debug = False
