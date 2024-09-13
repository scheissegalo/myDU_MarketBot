using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class RecipeService : IRecipeService
{
    private readonly ILogger<RecipeService> _logger;
    private const string ResourcesFilePath = "Data/resources.json";
    private const string RecipesFilePath = "Data/recipes.json";

    // Lazy<T> ensures that data is loaded only once, even with concurrent accesses
    private readonly Lazy<Task<List<Resource>>> _resources;
    private readonly Lazy<Task<List<Recipe>>> _recipes;

    public RecipeService(ILogger<RecipeService> logger)
    {
        _logger = logger;
        _resources = new Lazy<Task<List<Resource>>>(() => LoadDataAsync<Resource>(ResourcesFilePath));
        _recipes = new Lazy<Task<List<Recipe>>>(() => LoadDataAsync<Recipe>(RecipesFilePath));
    }

    // Returns cached resources if available, otherwise loads from file
    public async Task<List<Resource>> GetResourcesAsync()
    {
        return await _resources.Value;
    }

    // Returns cached recipes if available, otherwise loads from file
    public async Task<List<Recipe>> GetRecipesAsync()
    {
        return await _recipes.Value;
    }

    // Filter resources by tier
    public async Task<List<Resource>> GetResourcesByTier(int tier)
    {
        var resources = await GetResourcesAsync();
        return resources.FindAll(resource => resource.Tier == tier);
    }

    // Filter recipes by tier
    public async Task<List<Recipe>> GetRecipesByTier(int tier)
    {
        var recipes = await GetRecipesAsync();
        return recipes.FindAll(recipe => recipe.Tier == tier);
    }

    // Get recipes where the item is the product
    public async Task<List<Recipe>> GetRecipesItemIsProduct(ulong itemId)
    {
        var recipes = await GetRecipesAsync();
        return recipes.FindAll(recipe => recipe.Products.Exists(product => product.Id == itemId));
    }

    // Get recipes where the item is an ingredient
    public async Task<List<Recipe>> GetRecipesItemIsIngredient(ulong itemId)
    {
        var recipes = await GetRecipesAsync();
        return recipes.FindAll(recipe => recipe.Ingredients.Exists(ingredient => ingredient.Id == itemId));
    }

    // Get recipes where the item is an ingredient and recipe matches the given tier
    public async Task<List<Recipe>> GetRecipesItemIsIngredientTier(ulong itemId, int tier_of_recipe)
    {
        var recipes = await GetRecipesAsync();
        return recipes.FindAll(recipe => recipe.Tier == tier_of_recipe && recipe.Ingredients.Exists(ingredient => ingredient.Id == itemId));
    }

    public async Task<Resource> GetResourceAsync(ulong itemTypeId)
    {
        var resources = await GetResourcesAsync();
        return resources.Find(resource => resource.Id == itemTypeId);
    }

    // Private method to load data from JSON files
    private async Task<List<T>> LoadDataAsync<T>(string filePath)
    {
        try
        {
            var jsonData = await File.ReadAllTextAsync(filePath);
            return JsonConvert.DeserializeObject<List<T>>(jsonData);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error reading file {FilePath}", filePath);
            return new List<T>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON from file {FilePath}", filePath);
            return new List<T>();
        }
    }
}