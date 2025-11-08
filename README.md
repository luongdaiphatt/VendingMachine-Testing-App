# Vending Machine Communication Handler

**Vending Machine Communication Handler** là dự án giúp giao tiếp với máy bán hàng tự động (VMC) qua cổng COM hoặc máy VMC ảo. Hệ thống hỗ trợ:  
- Gửi lệnh tới máy VMC  
- Nhận dữ liệu trạng thái kênh, báo cáo xuất hàng  
- Logging và debug trực quan  
---

## 📌 Yêu cầu

- .NET 8
- Máy tính có cổng COM hoặc USB-to-COM  
- Visual Studio 2022 hoặc tương đương  

---

## 🔧 Cài đặt

1. Clone dự án:
```bash
git clone https://github.com/yourusername/vending-machine-comm.git
cd vending-machine-comm
2. Mở solution trong Visual Studio.
3. Build dự án: Ctrl + Shift + B

Nhập kênh hàng muốn truy vấn theo format: Hàng, Cột, Số lượng
