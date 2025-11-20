
# 🚀 DockerHome  
### Lightweight Docker Application Dashboard

DockerHome is a tiny self-hosted web interface as a link/home-page for all your docker-containers.
It lets you autogenerate a view and configure it all from a clean modern UI, similiar to HomeLab, not as complex and way simpler to set up.

Most of this stuff was vibecoded, its just a sideproject I created because I needed such a tool. It is only intedned to be run in Homelabs or Testservers, there is no security.

## ✨ Features

- 🧩 Automatic container discovery  
- 🎛️ Edit per‑app settings (icons, URLs, names)  
- 🖼️ Live icon preview with fallback  
- 🌙 Light & dark mode (system-aware)  
- 📦 Zero external dependencies  
- 🧹 Clean modern UI (2025 style)  
- 🐳 Docker‑first deployment  

---

## 📦 Quick Start (Docker)

Run with Docker:

```sh
docker run -d \
  --name dockerhome \
  -p 80:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v $(pwd)/data:/app/data \
  peerfunk/dockerhome:latest
```

Open in browser:

```
http://localhost
```

---

## 🖥 Development Setup

```sh
git clone https://github.com/peerfunk/DockerHome.git
cd DockerHome
dotnet run
```

---

## 📝 API (excerpt)

```
GET  /api/config
POST /api/config
GET  /api/apps
```

---

## 📜 License

MIT License — see the `LICENSE` file.

---

## 🤝 Contributing

1. Fork the repo  
2. Create a feature branch  
3. Commit changes  
4. Open a Pull Request  

---

## ⭐ Support

If you find DockerHome useful, please star the repo — it helps a lot.
