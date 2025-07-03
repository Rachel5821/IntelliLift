namespace IntelliLift
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. הגדרת שם למדיניות CORS
            var MyAllowSpecificOrigins = "_myAllowSpecificOrigins"; // שם למדיניות CORS

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // 2. הוספת שירותי CORS וקונפיגורציה של המדיניות
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins, // שם המדיניות
                                  policy =>
                                  {
                                      // זהו הפתרון הפתוח ביותר, מתאים לפיתוח
                                      // מאפשר כל Origin, כל Header, כל Method
                                      policy.AllowAnyOrigin()
                                            .AllowAnyHeader()
                                            .AllowAnyMethod();

                                      // חלופה מומלצת יותר עבור פרודקשן או אם את יודעת את ה-Origin המדויק:
                                      // policy.WithOrigins("http://localhost:3000", // ה-Origin של ה-React Front-end שלך
                                      //                   "https://myproductionapp.com") // אם יש לך דומיין פרודקשן
                                      //       .AllowAnyHeader()
                                      //       .AllowAnyMethod(); // או WithMethods("GET", "POST", "PUT", "DELETE")
                                  });
            });


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // 3. שימוש ב-CORS במערכת ה-middleware של ה-HTTP request
            // חשוב: UseCors חייב לבוא אחרי UseRouting ולפני UseAuthorization/UseEndpoints
            app.UseCors(MyAllowSpecificOrigins); // שימוש במדיניות שהגדרנו

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}