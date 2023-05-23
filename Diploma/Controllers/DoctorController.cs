using Diploma.Database;
using Diploma.model.user;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Diploma.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorController : ControllerBase
    {
        private EfModel _efModel;

        public DoctorController(EfModel model)
        {
            _efModel = model;
        }

        [HttpGet]
        public async Task<ActionResult<List<Doctor>>> GetAll(string? search, int? postId)
        {
            IQueryable<Doctor> doctors = _efModel.Doctors.Include(u => u.Post);

            if(search != null)
            {
                doctors = doctors.Where(u =>
                u.FirstName.Contains(search)
                || u.MidleName.Contains(search) || u.LastName.Contains(search));
            }

            if (postId != null)
            {
                doctors = doctors.Where(u => u.Post.Id == postId);
            }

            return await doctors.ToListAsync();
        }

        [Authorize(Roles = "AdminUser")]
        [HttpPut("{id}")]
        public async Task<ActionResult> Update(int id, UpdateDoctorDTO dto)
        {
            var doctor = await _efModel.Doctors
                .Include(u => u.Post)
                .FirstOrDefaultAsync(u => u.Id == id);
            
            if (doctor == null)
                return NotFound();

            var post = await _efModel.PostDoctors.FindAsync(dto.PostId);

            if (post == null)
                return NotFound();

            doctor.Offece = dto.Offece;
            doctor.MidleName = dto.MidleName;
            doctor.LastName = dto.LastName;
            doctor.FirstName = dto.FirstName;
            doctor.Post = post;

            _efModel.Entry(doctor).State = EntityState.Modified;
            await _efModel.SaveChangesAsync();

            return Ok();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var doctor = await _efModel.Doctors.FindAsync(id);

            if (doctor == null)
                return NotFound();

            _efModel.Doctors.Remove(doctor);
            await _efModel.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Doctor>> GetById(int id)
        {
            var doctor = await _efModel.Doctors
                .Include(u => u.Post)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (doctor == null)
                return NoContent();

            return doctor;
        }

        [HttpGet("Post")]
        public async Task<ActionResult<List<PostDoctor>>> GetAllPost(string? search)
        {
            IQueryable<PostDoctor> posts = _efModel.PostDoctors;

            if(search != null)
            {
                posts = posts.Where(u => search.Contains(u.Name));
            }

            return await posts.ToListAsync();
        }

        [Authorize(Roles = "AdminUser")]
        [HttpPost("Post")]
        public async Task<ActionResult<PostDoctor>> DoctorAddPost(string name)
        {
            var post = new PostDoctor
            {
                Name = name
            };

            await _efModel.PostDoctors.AddAsync(post);
            await _efModel.SaveChangesAsync();

            return post;
        }

        [HttpGet("Appointment")]
        public async Task<ActionResult<List<Appointment>>> GetAllAppointment(
            int? doctorId, int? pacientId, DateTime? startDate, DateTime? endDate)
        {
            IQueryable<Appointment> appointments = _efModel.Appointments
                .Include(u => u.Doctor)
                    .ThenInclude(u => u.Post)
                .Include(u => u.Pacient);

            if(doctorId != null)
            {
                appointments = appointments.Where(u => u.Doctor.Id == doctorId);
            }

            if(pacientId != null)
            {
                appointments = appointments
                    .Where(u => u.Pacient != null)
                    .Where(u => u.Pacient.Id == pacientId);
            }

            if(startDate != null)
            {
                appointments = appointments.Where(u => u.DateTime >= startDate);
            }

            if(endDate != null)
            {
                appointments = appointments.Where(u => u.DateTime <= endDate);
            }

            return await appointments.ToListAsync();
        }

        [Authorize]
        [HttpPost("{doctorId}/Appointment")]
        public async Task<ActionResult> AddAppointment(DateTime dateTime, int doctorId)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;

            if (identity == null)
                return NotFound();

            int idUser = Convert.ToInt32(identity.FindFirst("Id").Value);

            var user = await _efModel.Users.FindAsync(idUser);

            if (user == null)
                return NoContent();

            var doctor = await _efModel.Doctors.FindAsync(doctorId);

            if (doctor == null)
                return NoContent();
           
            var appointment = new Appointment
            {
                DateTime = dateTime,
                Doctor = doctor,
                Pacient = user
            };

            await _efModel.Appointments.AddAsync(appointment);
            await _efModel.SaveChangesAsync();

            return Ok();
        }

        [Authorize]
        [HttpPut("{doctorId}/Appointment/{id}")]
        public async Task<ActionResult<Appointment>> UpdateAppointments(int doctorId, int id, DateTime dateTime)
        {
            var appointment = await _efModel.Appointments.FindAsync(id);

            if (appointment == null)
                return NoContent();

            var doctor = await _efModel.Doctors.FindAsync(doctorId);

            if (doctor == null)
                return NotFound();

            appointment.DateTime = dateTime;
            appointment.Doctor = doctor;

            _efModel.Entry(appointment).State = EntityState.Modified;
            await _efModel.SaveChangesAsync();

            return appointment;
        }

        [Authorize]
        [HttpDelete("Appointment/{id}")]
        public async Task<ActionResult> DeleteAppointmentPacient(int id)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;

            if (identity == null)
                return NotFound();

            int idUser = Convert.ToInt32(identity.FindFirst("Id").Value);

            var user = await _efModel.Users.FindAsync(idUser);

            if (user == null)
                return NoContent();

            var appointment = await _efModel.Appointments
                .Include(u => u.Pacient)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (appointment == null)
                return NoContent();

            if (user.Role == "PatientUser")
            {
                if (appointment.Pacient.Id != user.Id)
                    return Unauthorized();
            }

            _efModel.Appointments.Remove(appointment);
            await _efModel.SaveChangesAsync();

            return Ok();
        }

        [Authorize(Roles = "DoctorUser")]
        [HttpPost("Recipe")]
        public async Task<ActionResult> AddRecipe(int patientid, string medicationText)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;

            if (identity == null)
                return NotFound();

            int idUser = Convert.ToInt32(identity.FindFirst("Id").Value);

            var doctor = await _efModel.Doctors.FindAsync(idUser);

            if (doctor == null)
                return NoContent();

            var patient = await _efModel.Users.FindAsync(patientid);

            if (patient == null)
                return NoContent();

            var recipe = new Recipe
            {
                Doctor = doctor,
                Patient = patient,
                MedicationText = medicationText
            };

            await _efModel.Recipes.AddAsync(recipe);
            await _efModel.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("Recipe")]
        public async Task<ActionResult<List<Recipe>>> GetAllRecipe(
            int? doctorId, int? patientId)
        {
            IQueryable<Recipe> recipes = _efModel.Recipes
                .Include(u => u.Doctor)
                .Include(u => u.Patient);

            if (doctorId != null)
            {
                recipes = recipes.Where(u => u.Doctor.Id == doctorId);
            }

            if(patientId != null)
            {
                recipes = recipes.Where(u => u.Patient.Id == patientId);
            }

            return await recipes.ToListAsync();
        }

        [Authorize(Roles = "DoctorUser")]
        [HttpPut("Recipe/{id}")]
        public async Task<ActionResult<Recipe>> UpdateRecipe(int id, UpdateRecipeDto dto)
        {
            var recipe = await _efModel.Recipes.FindAsync(id);

            if (recipe == null)
                return NotFound();

            var patiwnt = await _efModel.Users.FindAsync(dto.PatientId);

            if (patiwnt == null)
                return NotFound();

            recipe.Patient = patiwnt;
            recipe.MedicationText = dto.MedicationText;

            _efModel.Entry(recipe).State = EntityState.Modified;
            await _efModel.SaveChangesAsync();

            return recipe;
        }

        [Authorize(Roles = "DoctorUser")]
        [HttpDelete("Recipe/{id}")]
        public async Task<ActionResult> DeleteRecipr(int id)
        {
            var recipe = await _efModel.Recipes.FindAsync(id);

            if (recipe == null)
                return NotFound();

            _efModel.Recipes.Remove(recipe);
            await _efModel.SaveChangesAsync();

            return Ok();
        }
    }
}
