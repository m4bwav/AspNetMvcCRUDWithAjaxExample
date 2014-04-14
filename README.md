AspNet Mvc 3 CRUD With Ajax Example
============================

An example of a simple CRUD website with an ajax call using Asp.net MVC.

I created a one table, 6 column Sql Compact database and then used Entity Framework's Model-First approach to generate an edmx model.

Next I used the edmx model to add a ADO.NET DbContext Generator to create the data infrastructure. Then I cut and pasted the auto-generated classes directly into web project, and removed the code generation .tt files.  Since I won't need to regen the model again, those code generation files won't be needed.

The biggest challenge was re-installing visual studio 2010, in this case Visual Web Developer 2010 express, and all the necessary packages.  I considered using an EF Code First approach, but I figured I would go Model-First since I haven't used that approach as often with EF.

I used jquery ui's Date Picker, and the latest bootstrap for styling.
