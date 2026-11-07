"""One-time Windows setup; never sends an API request."""
import json
import os
import shutil
from pathlib import Path
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import sound_queue as queue


def main():
    app = tk.Tk()
    app.title('Chip Arena — ElevenLabs setup')
    app.resizable(False, False)
    if os.name != 'nt':
        messagebox.showerror('Windows required', 'Run this on the Windows builder PC.'); app.destroy(); return
    root = queue.home()
    config = root / 'config.json'
    old = json.loads(config.read_text()) if config.exists() else {}
    enabled = tk.BooleanVar(value=old.get('enabled', True))
    key = tk.StringVar()
    count = tk.StringVar(value=str(old.get('maxRequests', 10)))
    seconds = tk.StringVar(value=str(old.get('maxSeconds', 30)))
    folder = tk.StringVar(value=old.get('statusFolder', ''))
    box = ttk.Frame(app, padding=18); box.grid()
    ttk.Label(box, text='Use the same Windows account that runs the builder.\nApproved GitHub sound jobs use your ElevenLabs credits.\nSetup itself sends no requests. Limits apply to pending work per run.').grid(row=0, columnspan=3, sticky='w', pady=8)
    ttk.Checkbutton(box, text='Enable automatic approved sound jobs', variable=enabled).grid(row=1, columnspan=3, sticky='w')
    for row, label, value in [(2, 'ElevenLabs API key (blank keeps saved key)', key), (3, 'Maximum pending requests', count), (4, 'Maximum pending audio seconds', seconds), (5, 'Optional status folder (e.g. Drive build logs)', folder)]:
        ttk.Label(box, text=label).grid(row=row, column=0, sticky='w', pady=5)
        ttk.Entry(box, textvariable=value, width=42, show='*' if row == 2 else '').grid(row=row, column=1)
    def browse():
        selected = filedialog.askdirectory()
        if selected: folder.set(selected)
    ttk.Button(box, text='Browse', command=browse).grid(row=5, column=2)
    def save():
        try:
            requests, duration = int(count.get()), float(seconds.get())
            queue.limits(dict(maxRequests=requests, maxSeconds=duration))
            if enabled.get() and not shutil.which('ffmpeg'):
                raise ValueError('ffmpeg must be installed and available to the builder before enabling this.')
            if enabled.get() and not key.get().strip() and not (root / 'key.dpapi').exists():
                raise ValueError('Enter your ElevenLabs API key.')
            mirror = folder.get().strip()
            if mirror:
                queue.gen.outside_repo(Path(mirror))
                if not Path(mirror).is_dir(): raise ValueError('Choose an existing status folder.')
            root.mkdir(parents=True, exist_ok=True)
            if key.get().strip(): queue.gen.atomic(root / 'key.dpapi', queue.protect(key.get().strip().encode()))
            queue.write(config, dict(enabled=enabled.get(), maxRequests=requests, maxSeconds=duration, statusFolder=mirror))
            key.set('')
            messagebox.showinfo('Saved', 'Settings saved locally. The next builder asset install checks the approved queue.\nNo audio was generated during setup.')
            app.destroy()
        except (ValueError, OSError) as error:
            messagebox.showerror('Setup not saved', str(error))
    ttk.Button(box, text='Save', command=save).grid(row=6, column=1, sticky='e', pady=12)
    app.mainloop()

if __name__ == '__main__':
    main()
