// ============================================================================
// SHADOW CITY — Core/L10N.cs
// Bilingual EN/FA localization. Strings ported from the web build (which
// passed 100% parity audits). Persian output goes through PersianShaper.
// ============================================================================
using System.Collections.Generic;

namespace ShadowCity
{
    public static class L10N
    {
        public static string Lang = "en";
        public static bool IsRTL => Lang == "fa";

        public static void SetLanguage(string lang)
        {
            if (lang != "en" && lang != "fa") lang = "en";
            if (Lang == lang) return;
            Lang = lang;
            GameEvents.Emit(GameEvents.LanguageChanged, lang);
        }

        /// <summary>Translate; FA output is shaped for UGUI display.</summary>
        public static string T(string key)
        {
            if (!S.TryGetValue(key, out var pair)) return key;
            string raw = Lang == "fa" ? pair[1] : pair[0];
            return Lang == "fa" ? PersianShaper.Shape(PersianShaper.ToPersianDigits(raw)) : raw;
        }

        /// <summary>Translate with {0},{1} interpolation applied BEFORE shaping.</summary>
        public static string T(string key, params object[] args)
        {
            if (!S.TryGetValue(key, out var pair)) return key;
            string raw = string.Format(Lang == "fa" ? pair[1] : pair[0], args);
            return Lang == "fa" ? PersianShaper.Shape(PersianShaper.ToPersianDigits(raw)) : raw;
        }

        public static string Money(int n) =>
            Lang == "fa" ? T("_money", n.ToString("N0")) : "$" + n.ToString("N0");

        // ------------------------- STRING TABLE [en, fa] ----------------------
        public static readonly Dictionary<string, string[]> S = new()
        {
            {"_money", new[]{"${0}", "{0} دلار"}},
            {"game.title", new[]{"SHADOW CITY", "شهر سایه‌ها"}},
            {"game.subtitle", new[]{"Your deeds are written on its walls in light.", "کارهایت با نور بر دیوارهای شهر نوشته می‌شود."}},
            {"game.loading", new[]{"Building the city…", "در حال ساخت شهر…"}},
            {"menu.newGame", new[]{"New Game", "بازی جدید"}},
            {"menu.continue", new[]{"Continue", "ادامه"}},
            {"menu.language", new[]{"زبان: فارسی", "Language: English"}},
            {"menu.quality", new[]{"Quality: {0}", "کیفیت: {0}"}},
            {"menu.resume", new[]{"Resume", "ادامه بازی"}},
            {"menu.paused", new[]{"PAUSED", "توقف"}},
            {"menu.quit", new[]{"Quit to Menu", "خروج به منو"}},
            {"menu.save", new[]{"Save Game", "ذخیره بازی"}},

            {"hud.level", new[]{"LV {0}", "سطح {0}"}},
            {"hud.day", new[]{"Day {0}", "روز {0}"}},
            {"hud.speed", new[]{"{0} km/h", "{0} کیلومتر/ساعت"}},
            {"hud.enterVehicle", new[]{"[E] Enter vehicle", "[E] سوار شدن"}},
            {"hud.exitVehicle", new[]{"[E] Exit vehicle", "[E] پیاده شدن"}},
            {"hud.shop", new[]{"[E] Shop", "[E] فروشگاه"}},
            {"hud.mission", new[]{"[E] Start: {0}", "[E] شروع: {0}"}},
            {"hud.pulseHint", new[]{"Press Q — the city answers.", "دکمه Q را بزن — شهر پاسخ می‌دهد."}},

            {"phase.DAWN", new[]{"Dawn", "سپیده‌دم"}},
            {"phase.DAY", new[]{"Day", "روز"}},
            {"phase.DUSK", new[]{"Dusk", "گرگ‌ومیش"}},
            {"phase.NIGHT", new[]{"Night", "شب"}},

            {"district.DOWNTOWN", new[]{"Downtown", "مرکز شهر"}},
            {"district.NEON_STRIP", new[]{"Neon Strip", "خیابان نئون"}},
            {"district.OLD_QUARTER", new[]{"Old Quarter", "محله کهنه"}},
            {"district.HARBOR", new[]{"Harbor", "بندرگاه"}},
            {"district.HILLS", new[]{"The Hills", "تپه‌ها"}},

            {"res.feared", new[]{"{0} fears you.", "{0} از تو می‌ترسد."}},
            {"res.respected", new[]{"{0} respects you.", "{0} به تو احترام می‌گذارد."}},

            {"rpg.levelUp", new[]{"LEVEL UP — {0}", "ارتقاء سطح — {0}"}},
            {"rpg.xp", new[]{"+{0} XP", "+{0} تجربه"}},
            {"rpg.skills", new[]{"Skills", "مهارت‌ها"}},
            {"rpg.points", new[]{"Points: {0}", "امتیاز: {0}"}},
            {"rpg.dayTree", new[]{"DAY — Honest Path", "روز — مسیر شرافت"}},
            {"rpg.nightTree", new[]{"NIGHT — Shadow Path", "شب — مسیر سایه"}},

            {"skill.charm", new[]{"Charm", "جذبه"}},
            {"skill.trade", new[]{"Trade", "تجارت"}},
            {"skill.endurance", new[]{"Endurance", "استقامت"}},
            {"skill.stealth", new[]{"Stealth", "اختفا"}},
            {"skill.gunplay", new[]{"Gunplay", "تیراندازی"}},
            {"skill.driving", new[]{"Driving", "رانندگی"}},

            {"police.wantedUp", new[]{"Wanted level increased", "سطح تعقیب افزایش یافت"}},
            {"police.evaded", new[]{"You lost the police", "پلیس را گم کردی"}},
            {"police.busted", new[]{"BUSTED", "دستگیر شدی"}},
            {"police.wasted", new[]{"WASTED", "از پا درآمدی"}},
            {"police.respawn", new[]{"Continue", "ادامه"}},

            {"mission.taxi", new[]{"Taxi Shift", "شیفت تاکسی"}},
            {"mission.delivery", new[]{"Fragile Delivery", "تحویل شکستنی"}},
            {"mission.courier", new[]{"Courier Run", "پیک شهری"}},
            {"mission.race", new[]{"Midnight Circuit", "پیست نیمه‌شب"}},
            {"mission.robbery", new[]{"Register Job", "خالی کردن صندوق"}},
            {"mission.smuggle", new[]{"Harbor Run", "محموله بندر"}},
            {"mission.s1", new[]{"S1 — Out of Phase", "قسمت ۱ — خارج از فاز"}},
            {"mission.goto", new[]{"Go to the marker", "به نشانگر برو"}},
            {"mission.pickup", new[]{"Pick up the passenger", "مسافر را سوار کن"}},
            {"mission.dropoff", new[]{"Drop off at the marker", "در نشانگر پیاده کن"}},
            {"mission.deliver", new[]{"Deliver the package", "بسته را تحویل بده"}},
            {"mission.hold", new[]{"Hold position: {0}s", "سر جایت بمان: {0} ثانیه"}},
            {"mission.escape", new[]{"Escape the area!", "از منطقه فرار کن!"}},
            {"mission.checkpoint", new[]{"Checkpoint {0}/{1}", "ایست {0}/{1}"}},
            {"mission.usePulse", new[]{"Use the Echo Pulse (Q)", "از پالس پژواک استفاده کن (Q)"}},
            {"mission.complete", new[]{"MISSION COMPLETE", "ماموریت کامل شد"}},
            {"mission.failed", new[]{"MISSION FAILED", "ماموریت شکست خورد"}},
            {"mission.reward", new[]{"Reward: {0} · +{1} XP", "پاداش: {0} · +{1} تجربه"}},
            {"mission.timeLeft", new[]{"Time: {0}", "زمان: {0}"}},

            {"shop.medkit", new[]{"Medkit — {0}", "کیت درمان — {0}"}},
            {"shop.armor", new[]{"Body Armor — {0}", "زره بدن — {0}"}},
            {"shop.pistol", new[]{"Pistol — {0}", "کلت — {0}"}},
            {"shop.ammo", new[]{"Ammo — {0}", "مهمات — {0}"}},
            {"shop.noCash", new[]{"Not enough cash", "پول کافی نیست"}},
            {"shop.close", new[]{"Close", "بستن"}},

            {"notif.saved", new[]{"Game saved", "بازی ذخیره شد"}},
            {"notif.carStolen", new[]{"Vehicle acquired", "خودرو به دست آمد"}},
            {"notif.discovered", new[]{"Discovered: {0}", "کشف شد: {0}"}},

            {"cine.skip", new[]{"Press any key to skip", "برای رد شدن هر دکمه‌ای را بزن"}},

            {"story.s1.line", new[]{"The grid hums when you walk by. It knows you.", "شبکه برق هنگام عبورت زمزمه می‌کند. تو را می‌شناسد."}},
            {"story.endRespect", new[]{"The city glows gold where you walk. You are its keeper now.", "شهر هر جا قدم می‌گذاری طلایی می‌درخشد. تو حالا نگهبان آنی."}},
            {"story.endFear", new[]{"The neon burns red at your name. You are the city's shadow now.", "نئون‌ها با نام تو سرخ می‌سوزند. تو حالا سایه شهری."}},
        };
    }
}
