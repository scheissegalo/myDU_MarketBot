using System.Collections.Generic;

public class Recipe
{
    public long Id { get; set; }
    public int Tier { get; set; }
    public int Time { get; set; }
    public bool Nanocraftable { get; set; }
    public List<Ingredient> Ingredients { get; set; }
    public List<Product> Products { get; set; }
    public List<Producer> Producers { get; set; }
}
