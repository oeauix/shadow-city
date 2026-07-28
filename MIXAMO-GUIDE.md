# 🦴 راهنمای ریگ و انیمیشن با Mixamo (رایگان) — قدم‌به‌قدم
**زمان لازم: ~۲۰ دقیقه برای هر ۴ کاراکتر · هزینه: صفر**

Mixamo (متعلق به Adobe) همچنان رایگان است و خروجی‌هایش برای استفاده تجاری هم مجاز است.
کاراکترهای ما (character, cop, ped_man, ped_woman) عمداً **T-pose** ساخته شدند تا اینجا بدون دردسر ریگ شوند.

---

## مرحله ۰ — تبدیل GLB به FBX (چون Mixamo فقط FBX/OBJ می‌پذیرد)

ساده‌ترین راه، خود یونیتی است (Blender لازم نیست):
1. در یونیتی (که glTFast نصب است) روی `character.glb` در Project کلیک راست
2. اگر گزینه Export FBX نداری، ساده‌تر: **Blender رایگان** را نصب کن (blender.org) و:
   - File → Import → glTF 2.0 → `character.glb`
   - File → Export → FBX → تیک `Selected Objects` → ذخیره `character.fbx`
   - (برای هر ۴ کاراکتر تکرار — هر کدام ۳۰ ثانیه)

💡 اگر Blender هم نمی‌خواهی: سایت‌های تبدیل آنلاین GLB→FBX (مثل imagetostl.com/convert/file/glb/to/fbx) هم جواب می‌دهند.

---

## مرحله ۱ — آپلود و ریگ خودکار در Mixamo

1. برو به **mixamo.com** → ورود با حساب Adobe (ساختش رایگان است)
2. **Upload Character** → فایل `character.fbx`
3. مدل را بچرخان تا **رو به جلو** باشد
4. صفحه Auto-Rigger باز می‌شود: **۷ نشانگر** را بگذار روی: چانه، مچ دست چپ/راست، آرنج چپ/راست، زانو چپ/راست، و کشاله ران
5. Skeleton LOD: **Standard (65 bones)** — برای موبایل کافی و بهینه
6. Next → ۱-۲ دقیقه صبر کن → مدل ریگ‌شده در پیش‌نمایش می‌رقصد! ✅

⚠️ اگر خطای "cannot rig" داد: مدل ما مثلث‌های decimate شده دارد؛ در Blender یک `Remesh > Voxel 0.02` بزن و دوباره امتحان کن (به‌ندرت لازم می‌شود).

---

## مرحله ۲ — دانلود انیمیشن‌ها

برای بازی ما این ۶ کلیپ کافی است — در سرچ Mixamo بنویس و برای هرکدام:
تنظیمات دانلود: Format=**FBX for Unity** · Skin=**Without Skin** (به‌جز اولی) · FPS=30 · Keyframe Reduction=none

| # | جستجو در Mixamo | تنظیم مهم | نام فایل ذخیره |
|---|---|---|---|
| 1 | **Idle** | ✅ **With Skin** (این یکی مدل+اسکلت است) | `character_idle.fbx` |
| 2 | **Walking** | ✅ تیک **In Place** | `character_walk.fbx` |
| 3 | **Running** | ✅ تیک **In Place** | `character_run.fbx` |
| 4 | **Driving** (یا Sitting Idle) | — | `character_drive.fbx` |
| 5 | **Death** (مثلاً Dying Backwards) | — | `character_death.fbx` |
| 6 | **Pistol Idle** (اختیاری) | — | `character_aim.fbx` |

برای cop/ped_man/ped_woman: چون اسکلت Mixamo یکسان است، **لازم نیست دوباره انیمیشن بگیری** — فقط هر کدام را جدا آپلود+ریگ کن و فقط Idle-With-Skin را دانلود کن. انیمیشن‌های character برای همه کار می‌کنند (قدرت سیستم Humanoid یونیتی).

---

## مرحله ۳ — ست‌آپ در یونیتی (~۱۰ دقیقه)

1. پوشه بساز: `Assets/ShadowCity/Characters/` و FBX ها را داخلش بریز
2. برای **هر FBX**: انتخاب → Inspector → تب **Rig** → Animation Type = **Humanoid** → Apply
3. برای کلیپ‌های walk/run: تب **Animation** → تیک **Loop Time** → Apply
4. **Animator Controller** بساز: راست‌کلیک در Project → Create → Animator Controller → اسم: `CharacterAC`
5. دوبار کلیک روی CharacterAC (پنجره Animator باز می‌شود):
   - **Parameters** (سمت چپ، +): `Speed` (Float) · `IsDriving` (Bool) · `Dead` (Bool)
   - از FBX ها کلیپ‌های idle/walk/run را داخل گراف بکش
   - راست‌کلیک در فضای خالی → Create State → **From New Blend Tree** → اسمش Locomotion → دوبار کلیک:
     - Blend Parameter = `Speed`
     - Add Motion ×۳: idle (ترشولد 0) · walk (0.5) · run (1)
   - برگرد بالا؛ State های `Drive` و `Death` هم بساز (کلیپ‌هایشان را بکش)
   - Transition ها: `Any State → Drive` وقتی IsDriving=true · `Drive → Locomotion` وقتی false · `Any State → Death` وقتی Dead=true
6. حالا مدل‌ها: `character_idle.fbx` (نسخه With Skin) را بکش داخل صحنه موقتاً؟ **نه — به‌جایش:**
   - از آن یک **Prefab** بساز: بکش داخل `Assets/ShadowCity/Resources/Models/` و اسمش را بگذار `character` (جایگزین GLB قبلی — GLB را حذف یا rename کن)
   - روی Prefab: کامپوننت **Animator** → Controller = `CharacterAC`
   - همین کار برای cop/ped_man/ped_woman (همان CharacterAC برای همه!)
7. **Play** ▶ — کد بازی (`CharacterAnimator.cs` که الان اضافه کردم) خودش Animator را پیدا می‌کند و پارامترهای Speed/IsDriving/Dead را هر فریم می‌فرستد. هیچ کد دیگری لازم نیست.

### نتیجه:
- کاراکترها **راه می‌روند/می‌دوند** با انیمیشن موشن‌کپچر واقعی
- سوار ماشین = پوز رانندگی · مرگ = انیمیشن افتادن
- عابرها و پلیس هم همان کنترلر را می‌گیرند

---

## پلن B اگر Mixamo اذیت کرد
- **3daistudio.com/Tools/RiggingTool** — آپلود GLB مستقیم (بدون تبدیل FBX)، ریگ سازگار با Mixamo + انیمیشن‌های preset، خروجی FBX/GLB. چند کردیت رایگان اولیه دارد.
- **Tripo Rigging** — وقتی API شارژ شد: ریگ+انیمیشن هر کدام ~۰.۱$ از طریق همان Tripo Importer.
