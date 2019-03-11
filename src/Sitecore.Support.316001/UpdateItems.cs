using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.ExperienceForms;
using Sitecore.ExperienceForms.Client.Models.Builder;
using Sitecore.ExperienceForms.Client.Pipelines.SaveForm;
using Sitecore.ExperienceForms.Diagnostics;
using Sitecore.ExperienceForms.Extensions;
using Sitecore.ExperienceForms.Models;
using Sitecore.ExperienceForms.Mvc.Constants;
using Sitecore.ExperienceForms.Mvc.Extensions;
using Sitecore.Globalization;
using Sitecore.Mvc.Pipelines;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sitecore.ExperienceForms.Client.Pipelines.SaveForm
{
  public class UpdateItems : MvcPipelineProcessor<SaveFormEventArgs>
  {
    protected virtual ILogger Logger
    {
      get;
    }

    public UpdateItems(ILogger logger)
    {
      Assert.ArgumentNotNull(logger, "logger");
      Logger = logger;
    }

    public override void Process(SaveFormEventArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      if (args.ViewModelWrappers == null)
      {
        args.AddMessage(Translate.Text("There is no data in the view model wrappers list."));
      }
      else
      {
        ViewModelWrapper viewModelWrapper = args.ViewModelWrappers.FirstOrDefault((ViewModelWrapper m) => ID.Parse(m.Model.TemplateId) == TemplateIds.FormTemplateId);
        if (viewModelWrapper == null)
        {
          args.AddMessage(Translate.Text("The form model was not found in the view model wrappers collection."));
        }
        else
        {
          List<Item> list = new List<Item>();
          if (!SaveModel(viewModelWrapper, args, list))
          {
            args.AddMessage(Translate.Text("The form was not saved."));
          }
          else
          {
            list.ForEach(delegate (Item i)
            {
              i.Recycle();
            });
            args.Result = viewModelWrapper.Model.ItemId;
          }
        }
      }
    }

    protected virtual bool SaveModel(ViewModelWrapper viewModelWrapper, SaveFormEventArgs args, ICollection<Item> deleteList)
    {
      Assert.ArgumentNotNull(viewModelWrapper, "viewModelWrapper");
      Assert.ArgumentNotNull(args, "args");
      Assert.ArgumentNotNull(deleteList, "deleteList");
      ID iD = ID.Parse(viewModelWrapper.ParentId);
      if (!args.FormBuilderContext.Database.Items.Exists(iD))
      {
        args.AddMessage(Translate.Text("Item does not exist."));
        return false;
      }
      Item item = args.FormBuilderContext.Database.GetItem(iD, args.FormBuilderContext.Language);
      ID modelItemId = ID.Parse(viewModelWrapper.Model.ItemId);
      ID iD2 = modelItemId;
      Item item2;
      try
      {
        if (args.FormBuilderContext.Database.Items.Exists(iD2))
        {
          item2 = args.FormBuilderContext.Database.GetItem(iD2, args.FormBuilderContext.Language);
          if (args.FormBuilderContext.FormBuilderMode == FormBuilderMode.Copy)
          {
            iD2 = ID.NewID;
            Item item3 = item2;
            item2 = item3.CopyTo(item, viewModelWrapper.Model.Name, iD2, false);
            viewModelWrapper.Model.ItemId = item2.ID.ToClientIdString();
          }
          else
          {
            if (iD != item2.ParentID)
            {
              item2.MoveTo(item);
            }
            CollectDeletedChildren(deleteList, args.ViewModelWrappers, item2);
          }
        }
        else
        {
          item2 = AddItem(viewModelWrapper.Model.Name, item, new TemplateID(ID.Parse(viewModelWrapper.Model.TemplateId)), iD2);
        }
      }
      catch (Exception ex)
      {
        Logger.LogError(ex.Message, ex, this);
        args.AddMessage(Translate.Text("Failed to create an item with id {0}.", iD2));
        return false;
      }
      if (!EditModelProperties(viewModelWrapper, item2))
      {
        args.AddMessage(Translate.Text("Failed filling fields for item {0}.", iD2));
        return false;
      }
      IOrderedEnumerable<ViewModelWrapper> orderedEnumerable = from m in args.ViewModelWrappers
                                                               where ID.Parse(m.ParentId) == modelItemId
                                                               select m into s
                                                               orderby s.SortOrder
                                                               select s;
      foreach (ViewModelWrapper item4 in orderedEnumerable)
      {
        item4.ParentId = viewModelWrapper.Model.ItemId;
        if (!SaveModel(item4, args, deleteList))
        {
          return false;
        }
      }
      return true;
    }

    protected virtual Item AddItem(string itemName, Item destination, TemplateID templateId, ID newId)
    {
      Assert.ArgumentNotNullOrEmpty(itemName, "itemName");
      Assert.ArgumentNotNull(destination, "destination");
      Assert.ArgumentNotNull(templateId, "templateId");
      Assert.ArgumentNotNull(newId, "newId");
      return destination.Add(itemName, templateId, newId);
    }

    internal static bool EditModelProperties(ViewModelWrapper viewModelWrapper, Item item)
    {
      IDataItem dataItem = viewModelWrapper.Model as IDataItem;
      if (dataItem != null)
      {
        try
        {
          item.Editing.BeginEdit();
          if (!dataItem.UpdateItem(item))
          {
            return false;
          }
          item.Fields[FieldIDs.Sortorder].Value = (viewModelWrapper.SortOrder.HasValue ? viewModelWrapper.SortOrder.ToString() : "");
        }
        finally
        {
          item.Editing.EndEdit();
        }
      }
      return true;
    }

    private static void CollectDeletedChildren(ICollection<Item> deleteList, ICollection<ViewModelWrapper> viewModelWrappers, Item item)
    {
      if (!deleteList.Contains(item))
      {
        foreach (Item child in item.Children)
        {
          if (child.Template.IsBasedOnTemplate(TemplateIds.FieldTemplateId) && viewModelWrappers.FirstOrDefault((ViewModelWrapper w) => ID.Parse(w.Model.ItemId) == child.ID) == null)
          {
            deleteList.Add(child);
          }
        }
      }
    }
  }
}