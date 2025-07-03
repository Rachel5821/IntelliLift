namespace IntelliLift
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. ����� �� �������� CORS
            var MyAllowSpecificOrigins = "_myAllowSpecificOrigins"; // �� �������� CORS

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // 2. ����� ������ CORS ������������ �� ��������
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins, // �� ��������
                                  policy =>
                                  {
                                      // ��� ������ ����� �����, ����� ������
                                      // ����� �� Origin, �� Header, �� Method
                                      policy.AllowAnyOrigin()
                                            .AllowAnyHeader()
                                            .AllowAnyMethod();

                                      // ����� ������ ���� ���� ������� �� �� �� ����� �� �-Origin ������:
                                      // policy.WithOrigins("http://localhost:3000", // �-Origin �� �-React Front-end ���
                                      //                   "https://myproductionapp.com") // �� �� �� ������ �������
                                      //       .AllowAnyHeader()
                                      //       .AllowAnyMethod(); // �� WithMethods("GET", "POST", "PUT", "DELETE")
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

            // 3. ����� �-CORS ������ �-middleware �� �-HTTP request
            // ����: UseCors ���� ���� ���� UseRouting ����� UseAuthorization/UseEndpoints
            app.UseCors(MyAllowSpecificOrigins); // ����� �������� �������

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}