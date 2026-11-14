# Cơ sở dữ liệu

| Tệp | Nội dung |
|---|---|
| [`schema.sql`](schema.sql) | Lược đồ đầy đủ 34 bảng (25 bảng `public` + 9 bảng `auth`), 263 cột, 53 khóa ngoại, ràng buộc CHECK / UNIQUE / EXCLUDE và chỉ mục. Không chứa dữ liệu. |
| [`ERD.md`](ERD.md) | ERD dạng cây theo 6 nhóm bảng và bảng tổng hợp 53 quan hệ. |
| [`../docs/03-thiet-ke/so-do/erd/`](../docs/03-thiet-ke/so-do/erd/) | ERD chi tiết dạng hình (Crow's Foot). |
| [`../docs/03-thiet-ke/tu-dien-du-lieu.md`](../docs/03-thiet-ke/tu-dien-du-lieu.md) | Từ điển dữ liệu từng cột. |
| [`migrations/`](migrations/) | Script nâng cấp lược đồ theo từng giai đoạn phát triển (đã gộp trong `schema.sql`, không cần chạy lại). |
| [`queries/`](queries/) | Truy vấn phân tích S1–S4 dùng để đối chiếu kết quả trực tiếp trên CSDL. |

## Dựng CSDL trống

```bash
createdb harmonia
psql -d harmonia -v ON_ERROR_STOP=1 -f database/schema.sql
```

`schema.sql` được sinh từ catalog lược đồ của CSDL phát triển và đã được chạy thử trên một CSDL PostgreSQL trống (kết quả: 34 bảng, 263 cột, 53 khóa ngoại, 2 ràng buộc EXCLUDE). Cần extension `btree_gist` cho ràng buộc EXCLUDE; script đã tự tạo.

## Điểm thiết kế chính

- **Dữ liệu có thời hạn:** `ClassMembership` và `TeacherClassAssignment` lưu khoảng `[StartDate, EndDate)`; ràng buộc `EXCLUDE USING gist` chặn chồng lấn ngay trong CSDL.
- **Bản chụp (snapshot):** `QuestionInTest`, `InterventionPackageTheory`, `InterventionPackageQuestion` lưu nội dung và Subskill tại thời điểm tạo (cột tiền tố `SnS`), nên sửa ngân hàng câu hỏi không làm sai kết quả cũ.
- **Nhập nguyên tử:** mỗi lần nhập CSV là một `ImportBatch`; `Attempt` duy nhất theo (học sinh, đợt), `Response` duy nhất theo (lượt làm, câu).
- **Lịch sử nhận xét:** `StudentReportComment` có số phiên bản, tự tham chiếu tới bản đã duyệt, chỉ mục duy nhất có điều kiện cho bản Draft / Approved và CHECK buộc nguồn Gemini phải lưu bản gốc AI.
