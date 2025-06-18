# 📚 OnlineLibrary – ASP.NET MVC Digital Book System

Welcome to **OnlineLibrary**, a full-stack ASP.NET MVC web application that allows users to register, log in, borrow books, pay online (via Stripe/PayPal), and manage their digital reading experience.

---

## ✅ Step-by-Step Instructions to Clone & Run the Project

### 🧰 1. Prerequisites

Before starting, make sure you have these installed:

| Tool                     | Description                                           |
|--------------------------|-------------------------------------------------------|
| ✅ Visual Studio 2019/2022 | IDE to open and run the ASP.NET MVC solution         |
| ✅ Microsoft SQL Server    | To create the `OnlineLibrary` database               |
| ✅ SSMS (SQL Server Management Studio) | To manage and run SQL scripts               |
| ✅ .NET Framework 4.7.2    | Required target framework                            |
| ✅ Git                    | To clone the repository                              |

### 📥 2. Clone the Project

```bash
git clone https://github.com/MohammedGhara/EbookLibrary.git
cd EbookLibrary
```

### 📂 3. Open the Project in Visual Studio

1. Open `OnlineLibrary.sln` using Visual Studio.
2. Wait for the NuGet packages to restore automatically.
3. If needed, right-click the `OnlineLibrary` project and select **"Set as Startup Project"**.

### 🗄️ 4. Create the SQL Server Database

#### A. Open SSMS (SQL Server Management Studio)
1. Create a new database named `OnlineLibrary`


### ⚙️ 5. Configure the Database Connection

Go to `appsettings.json` or `Web.config` in the project, and update the connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=OnlineLibrary;Trusted_Connection=True;"
}
```

Replace `YOUR_SERVER_NAME` with your SQL Server instance (e.g. `DESKTOP-ABC123\SQLEXPRESS`).

📚 Calibre Integration – E-book Conversion

This project integrates with Calibre to convert uploaded eBooks into different formats (e.g., PDF, EPUB, MOBI). To enable this functionality, follow the steps below:

✅ Step 1: Download & Install Calibre (64-bit)
[Visit: https://calibre-ebook.com/download/windows](https://calibre-ebook.com/download_windows)

Download the 64-bit version for Windows.

"CalibreSettings": {
  "CalibrePath": "C:\\Program Files\\Calibre2\\ebook-convert.exe"
}

Install Calibre.
### ▶️ 6. Run the Application

In Visual Studio:
1. Press `F5` or click the green **Start** button
2. The web browser will open
3. You will land on the **Login Page**

### 👤 7. Use the System (Example Flow)

- 📝 Sign up as a new user (enter your info + optional credit card)
- 🔐 Log in with your credentials
- 📖 Browse and borrow books
- 💳 Make a payment via **Stripe** or **PayPal**
- 📬 Forgot your password? Use the "Forgot Password" link to reset via email

### 🧪 8. Admin Side (Optional)

If you have an admin account, you can:
- Manage users
- Add/edit books
- View payments
- Monitor activity
Iam already Register by Running an admin account so you can try it :
Username:Admin
Password:1234
## 💻 Technologies Used

- ASP.NET MVC (.NET Framework 4.7.2)
- SQL Server + SSMS
- Entity Framework (Database First / Code First)
- Razor Views (.cshtml)
- Stripe & PayPal API Integration
- Bootstrap + CSS for UI

## 🛡️ Security & Warnings

- ✅ Secrets like Stripe keys and email passwords are scanned by GitHub.
- ❌ Don't commit real credentials.
- ✔️ Use `.gitignore` to prevent uploading `.vs/`, `*.user`, `bin/`, `obj/`, etc.

``

## 📌 License & Attribution

This project was created by **Mohammed Ghara**  
It is free to use for learning and academic purposes.

