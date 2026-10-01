namespace MODEL;

public class Admin
{
	public int Id { get; set; }

	public string Email { get; set; }

	public string Phone { get; set; }

	public string Password { get; set; }

	public string Name { get; set; }

	public string AvatarUrl { get; set; }

	public string Description { get; set; }

	public byte IsSuper { get; set; }

	public string HiddenColumnJson { get; set; }

	public string SkinName { get; set; }

	public byte ReLogin { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
