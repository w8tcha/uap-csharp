/**
 * Build process for YetAnotherForum.NET
 *
 * Don't know where to start?
 * Try: http://24ways.org/2013/grunt-is-not-weird-and-hard/
 */
module.exports = function(grunt) {


    // CONFIGURATION
    grunt.initConfig({
        pkg: grunt.file.readJSON('package.json'),

        downloadfile: {
            options: {
                dest: './UAParser.Core',
                overwriteEverytime: true
            },
            files: {
                'regexes.yaml': 'https://raw.githubusercontent.com/ua-parser/uap-core/master/regexes.yaml'
            }
        },
        
        devUpdate: {
            main: {
                options: {
                    reportUpdated: true,
                    updateType: 'force',
                    semver: false,
                    packages: {
                        devDependencies: true, //only check for devDependencies
                        dependencies: true
                    }
                }
            }
        }
    });

    // PLUGINS
    grunt.loadNpmTasks('@w8tcha/grunt-dev-update');
    grunt.loadNpmTasks('grunt-downloadfile');

    grunt.registerTask('default',
        [
            'downloadfile'
        ]);
};
