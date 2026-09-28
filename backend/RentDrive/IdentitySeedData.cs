using Microsoft.AspNetCore.Identity;
using RentDrive.db;
using RentDrive.db.models;
using Serilog;

namespace RentDrive
{
    public static class IdentitySeedData
    {
        private static readonly Serilog.ILogger logger = Log.ForContext(typeof(IdentitySeedData));

        public static async Task InitializeAsync(IServiceProvider serviceProvider, ApplicationDbContext context)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roleNames = ["Customer", "Moderator", "Admin"];
            foreach (var roleName in roleNames)
            {
                var roleExists = await roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.Information("Создана роль {role}", roleName);
                }
            }

            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            string adminEmail = "admin@rentdrive.com";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var newAdmin = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createAdminResult = await userManager.CreateAsync(newAdmin, "admin123");

                if (createAdminResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                    logger.Information("Добавлен админ с id: {id}, email: {email}", newAdmin.Id, adminEmail);
                }
            }

            if (context.RentItems.Any()) return;
            string ownerId = "7ddc03d3-fcd3-4e91-ba76-bc8dbb125066";
            var user = await userManager.FindByIdAsync(ownerId);
            if (user == null) return;

            var testItems = new List<RentItem>
            {
                // --- Категория: Бюджетные (до 50) ---
                new() { Title = "Уютная комната в центре", Description = "Небольшая комната для непритязательных путешественников. Рядом с метро.", PricePerDay = 25.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Хостел 'Подушка и Завтрак'", Description = "Койко-место в общем номере. Бесплатный Wi-Fi и чай.", PricePerDay = 15.50m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Студия на окраине", Description = "Тихий район, скромный интерьер, но есть все необходимое для жизни.", PricePerDay = 40.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Комната у вокзала", Description = "Удобно для ночевки между рейсами. Шумно, но дешево.", PricePerDay = 20.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Мини-отель 'Эконом'", Description = "Чистый номер с общим санузлом. До центра 20 минут на автобусе.", PricePerDay = 35.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Апартаменты 'Loft Скромный'", Description = "Стиль лофт, но бюджетный вариант. Подходит для студентов.", PricePerDay = 45.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Домик в деревне", Description = "Дачный вариант для отдыха от городской суеты. Удобства на улице.", PricePerDay = 30.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Капсульный отель 'Future'", Description = "Современная капсула со сном в футуристичном стиле.", PricePerDay = 18.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Квартира-студия 'Компакт'", Description = "Очень маленькая, но своя. Есть мини-кухня.", PricePerDay = 38.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Старая добрая хрущевка", Description = "Простая квартира со старым ремонтом, но очень гостеприимная.", PricePerDay = 28.00m, OwnerId = "ownerId", Owner = user },

                // --- Категория: Средний класс (50 - 150) ---
                new() { Title = "Современная квартира-студия", Description = "Свежий ремонт, кондиционер, вид на парк. Отличный выбор для пар.", PricePerDay = 75.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Семейные апартаменты в спальном районе", Description = "Две комнаты, большая кухня, детская площадка во дворе.", PricePerDay = 90.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Пляжный домик 'Бриз'", Description = "Первая линия, до пляжа 50 метров. Слышен шум моря.", PricePerDay = 120.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Апартаменты 'Сканди' в центре", Description = "Светлый интерьер в скандинавском стиле. Все достопримечательности пешком.", PricePerDay = 110.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Уютный таунхаус", Description = "Два этажа, собственный небольшой дворик с барбекю.", PricePerDay = 140.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Отель 'Бизнес Класс'", Description = "Стандартный номер с рабочим столом и быстрым интернетом.", PricePerDay = 95.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Квартира с панорамным видом", Description = "18 этаж, красивый вид на вечерний город. Романтическое место.", PricePerDay = 130.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Дом у озера", Description = "Прекрасное место для рыбалки и отдыха на природе. Есть баня.", PricePerDay = 150.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Апартаменты 'Ретро'", Description = "Интерьер в стиле 80-х, но со всеми современными удобствами.", PricePerDay = 65.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Студия 'Зеленый Квартал'", Description = "Экологически чистый район, вокруг много зелени и велодорожек.", PricePerDay = 80.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Апартаменты 'Neon Loft'", Description = "Яркий дизайн с неоновой подсветкой для стильных фотоссесий.", PricePerDay = 85.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Квартира 'Тихий дворик'", Description = "В самом центре, но окна выходят в закрытый тихий двор.", PricePerDay = 105.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Коттедж 'Лесной'", Description = "Небольшой деревянный коттедж на опушке леса.", PricePerDay = 125.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Апартаменты 'Морской вокзал'", Description = "Рядом с портом, красивый вид на корабли из окна.", PricePerDay = 115.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Студия 'Минимализм'", Description = "Ничего лишнего, много пространства и света.", PricePerDay = 70.00m, OwnerId = "ownerId", Owner = user },

                // --- Категория: Премиум и Люкс (от 200) ---
                new() { Title = "Пентхаус 'Люкс' с бассейном", Description = "Роскошный пентхаус на крыше. Собственный бассейн и терраса.", PricePerDay = 550.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Вилла 'Grand Palace'", Description = "Огромная вилла для больших компаний. Охраняемая территория, люкс сервис.", PricePerDay = 800.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Президентский люкс в пятизвездочном отеле", Description = "Эксклюзивный сервис, классический дизайн, мраморная ванная.", PricePerDay = 1200.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Дизайнерские апартаменты премиум класса", Description = "Мебель от известных дизайнеров, система 'Умный дом'.", PricePerDay = 350.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Шале 'Альпийская сказка'", Description = "Элитное шале в горах с камином и панорамным остеклением.", PricePerDay = 450.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Вилла 'Sunset View'", Description = "Прекрасный вид на закат, собственный выход к причалу.", PricePerDay = 600.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Исторический особняк", Description = "Проживание в настоящем отреставрированном замке XIX века.", PricePerDay = 950.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Пентхаус 'Skyline Luxury'", Description = "Вид на небоскребы с высоты птичьего полета. Джакузи на террасе.", PricePerDay = 700.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Апартаменты 'Gold & Marble'", Description = "Дорогой интерьер с элементами золота и натурального мрамора.", PricePerDay = 400.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Эко-вилла класса люкс", Description = "Полностью автономная вилла из экологических материалов премиум уровня.", PricePerDay = 500.00m, OwnerId = "ownerId", Owner = user },

                // --- Края для проверки фильтрации цен и пустых строк ---
                new() { Title = "Самый дешевый угол", Description = "Тест минимальной цены.", PricePerDay = 5.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Самый дорогой дворец", Description = "Тест максимальной цены.", PricePerDay = 5000.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Специфический_Поиск_Тест", Description = "Объект со сложным словом для точечного поиска.", PricePerDay = 100.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = " ", Description = "Проверка обработки пустых названий (edge case).", PricePerDay = 50.00m, OwnerId = "ownerId", Owner = user },
                new() { Title = "Апартаменты Тест Фильтра", Description = "Объект для проверки крайних значений фильтрации.", PricePerDay = 150.00m, OwnerId = "ownerId", Owner = user }
            };

            context.RentItems.AddRange(testItems);
            await context.SaveChangesAsync();
        }
    }
}
