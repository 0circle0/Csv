using System.ComponentModel.DataAnnotations;

namespace CsvRunner.Run
{
    public class Person
    {
        [Display(Name = "ID"), Required]
        public Guid Id { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Age")]
        public int Age { get; set; }

        [Display(Name = "Hire Date")]
        public DateTime HireDate { get; set; }

        public override string ToString()
        {
            return $"FirstName: {FirstName}, LastName: {LastName}, FullName: {FullName}, Age: {Age}\nID: {Id}\nAddress: {Address}\nHire Date: {HireDate}";
        }
    }
}
