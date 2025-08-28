-- Training Mode Plus :]

-- getting the TrainingManager singleton also helps for determining SF6 initialization
local TrainingManager = nil

re.on_frame(function ()
    if not TrainingManager then
        -- objective here is to get the singletons to check training status
        TrainingManager = sdk.get_managed_singleton("app.training.TrainingManager")
        if TrainingManager then
            -- initialize data?


        else
            -- return here to not execute the rest of the on_frame function
            -- not needed but I'd rather not have indentation on the function body
            return
        end
    end

    -- if we reach this point then we have the training manager
    -- we can now check if we are in training mode
    if TrainingManager._TrainingState == 1 then 


    end

end)